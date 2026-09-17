using System.Globalization;
using System.Text;
using Serilog.Core;
using Serilog.Parsing;

// ReSharper disable PossibleMultipleEnumeration

namespace Reaparr.FluentResults
{
    public partial class Result
    {
        static Result()
        {
            Settings = new ResultSettingsBuilder().Build();
        }

        internal static ResultSettings Settings { get; private set; }

        /// <summary>
        ///     Setup global settings like logging
        /// </summary>
        public static void Setup(Action<ResultSettingsBuilder> setupFunc)
        {
            var settingsBuilder = new ResultSettingsBuilder();
            setupFunc(settingsBuilder);

            Settings = settingsBuilder.Build();
        }

        /// <summary>
        ///     Creates a success result
        /// </summary>
        public static Result Ok()
        {
            return new Result();
        }

        /// <summary>
        ///     Creates a failed result with the given error
        /// </summary>
        public static Result Fail(IError error)
        {
            var result = new Result();
            result.WithError(error);
            return result;
        }

        /// <summary>
        ///     Creates a failed result with the given error message. Internally an error object from the error factory is created.
        /// </summary>
        [MessageTemplateFormatMethod("errorMessage")]
        public static Result Fail(string errorMessage, params object[] args)
        {
            var result = new Result();
            result.WithError(Settings.ErrorFactory(FormatErrorMessage(errorMessage, args)));
            return result;
        }

        private static string FormatErrorMessage(string messageTemplate, object[] args)
        {
            if (args.Length == 0)
                return messageTemplate;

            var parsedTemplate = new MessageTemplateParser().Parse(messageTemplate);
            var result = new StringBuilder(messageTemplate.Length);
            var nextArgument = 0;

            foreach (var token in parsedTemplate.Tokens)
            {
                if (token is TextToken textToken)
                {
                    result.Append(textToken.Text);
                    continue;
                }

                var propertyToken = (PropertyToken)token;
                object? value;
                if (propertyToken.TryGetPositionalValue(out var position))
                {
                    if (position < 0 || position >= args.Length)
                    {
                        result.Append(propertyToken);
                        continue;
                    }

                    value = (object?)args[position];
                }
                else
                {
                    if (nextArgument >= args.Length)
                    {
                        result.Append(propertyToken);
                        continue;
                    }

                    value = (object?)args[nextArgument++];
                }

                var valueText = value switch
                {
                    IFormattable formattable => formattable.ToString(propertyToken.Format, CultureInfo.InvariantCulture),
                    null => string.Empty,
                    _ => value.ToString() ?? string.Empty,
                };

                if (propertyToken.Alignment is { } alignment && valueText.Length < alignment.Width)
                {
                    valueText =
                        alignment.Direction == AlignmentDirection.Left
                            ? valueText.PadRight(alignment.Width)
                            : valueText.PadLeft(alignment.Width);
                }

                result.Append(valueText);
            }

            return result.ToString();
        }

        /// <summary>
        ///     Creates a failed result with the given error messages. Internally a list of error objects from the error factory is
        ///     created
        /// </summary>
        public static Result Fail(IEnumerable<string> errorMessages)
        {
            if (errorMessages == null)
                throw new ArgumentNullException(nameof(errorMessages), "The list of error messages cannot be null");

            if (!errorMessages.Any())
                throw new ArgumentException("The list of errors is empty", nameof(errorMessages));

            var result = new Result();
            result.WithErrors(errorMessages.Select(Settings.ErrorFactory));
            return result;
        }

        /// <summary>
        ///     Creates a failed result with the given errors.
        /// </summary>
        public static Result Fail(IEnumerable<IError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors), "The list of errors cannot be null");

            if (!errors.Any())
                throw new ArgumentException("The list of errors is empty", nameof(errors));

            var result = new Result();
            result.WithErrors(errors);
            return result;
        }

        /// <summary>
        ///     Creates a success result with the given value
        /// </summary>
        public static Result<TValue> Ok<TValue>(TValue value)
        {
            var result = new Result<TValue>();
            result.WithValue(value);
            return result;
        }

        /// <summary>
        ///     Creates a failed result with the given error
        /// </summary>
        public static Result<TValue> Fail<TValue>(IError error)
        {
            var result = new Result<TValue>();
            result.WithError(error);
            return result;
        }

        /// <summary>
        ///     Creates a failed result with the given error message. Internally an error object from the error factory is created.
        /// </summary>
        public static Result<TValue> Fail<TValue>(string errorMessage)
        {
            var result = new Result<TValue>();
            result.WithError(Settings.ErrorFactory(errorMessage));
            return result;
        }

        /// <summary>
        ///     Creates a failed result with the given error messages. Internally a list of error objects from the error factory is
        ///     created.
        /// </summary>
        public static Result<TValue> Fail<TValue>(IEnumerable<string> errorMessages)
        {
            if (errorMessages == null)
                throw new ArgumentNullException(nameof(errorMessages), "The list of error messages cannot be null");

            if (!errorMessages.Any())
                throw new ArgumentException("The list of errors is empty", nameof(errorMessages));

            var result = new Result<TValue>();
            result.WithErrors(errorMessages.Select(Settings.ErrorFactory));
            return result;
        }

        /// <summary>
        ///     Creates a failed result with the given errors.
        /// </summary>
        public static Result<TValue> Fail<TValue>(IEnumerable<IError> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors), "The list of errors cannot be null");

            if (!errors.Any())
                throw new ArgumentException("The list of errors is empty", nameof(errors));

            var result = new Result<TValue>();
            result.WithErrors(errors);
            return result;
        }

        /// <summary>
        ///     Merge multiple result objects to one result object together
        /// </summary>
        public static Result Merge(params ResultBase[] results)
        {
            return ResultHelper.Merge(results);
        }

        /// <summary>
        ///     Merge multiple result objects to one result object together. Return one result with a list of merged values.
        /// </summary>
        public static Result<IEnumerable<TValue>> Merge<TValue>(params Result<TValue>[] results)
        {
            return ResultHelper.MergeWithValue(results);
        }

        /// <summary>
        ///     Merge multiple result objects to one result object together. Return one result with a flattened list of merged
        ///     values.
        /// </summary>
        public static Result<IEnumerable<TValue>> MergeFlat<TValue, TArray>(params Result<TArray>[] results)
            where TArray : IEnumerable<TValue>
        {
            return ResultHelper.MergeWithValue<TValue, TArray>(results);
        }

        /// <summary>
        ///     Create a success/failed result if any error objects exist
        /// </summary>
        public static Result FailIfNotEmpty(IEnumerable<IError> errors)
        {
            return errors.Any() ? Fail(errors) : Ok();
        }

        /// <summary>
        ///     Create a success/failed result depending if any error objects exist
        /// </summary>
        /// <remarks>
        ///     Error is lazily evaluated.
        /// </remarks>
        public static Result FailIfNotEmpty<T>(IEnumerable<T> errors, Func<T, IError> func)
        {
            return errors.Any() ? Fail(errors.Select(error => func(error))) : Ok();
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static Result Try(Action action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                action();
                return Ok();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async Task<Result> Try(Func<Task> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                await action();
                return Ok();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async ValueTask<Result> Try(Func<ValueTask> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                await action();
                return Ok();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static Result<T> Try<T>(Func<T> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return Ok(action());
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async Task<Result<T>> Try<T>(Func<Task<T>> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return Ok(await action());
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async ValueTask<Result<T>> Try<T>(
            Func<ValueTask<T>> action,
            Func<Exception, IError>? catchHandler = null
        )
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return Ok(await action());
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static Result Try(Func<Result> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return action();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async Task<Result> Try(Func<Task<Result>> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return await action();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async ValueTask<Result> Try(
            Func<ValueTask<Result>> action,
            Func<Exception, IError>? catchHandler = null
        )
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return await action();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static Result<T> Try<T>(Func<Result<T>> action, Func<Exception, IError>? catchHandler = null)
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return action();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async Task<Result<T>> Try<T>(
            Func<Task<Result<T>>> action,
            Func<Exception, IError>? catchHandler = null
        )
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return await action();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }

        /// <summary>
        ///     Executes the action. If an exception is thrown within the action then this exception is transformed via the
        ///     catchHandler to an Error object
        /// </summary>
        public static async ValueTask<Result<T>> Try<T>(
            Func<ValueTask<Result<T>>> action,
            Func<Exception, IError>? catchHandler = null
        )
        {
            catchHandler = catchHandler ?? Settings.DefaultTryCatchHandler;

            try
            {
                return await action();
            }
            catch (Exception e)
            {
                return Fail(catchHandler(e));
            }
        }
    }
}
