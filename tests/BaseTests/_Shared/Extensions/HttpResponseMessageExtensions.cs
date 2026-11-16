using System.Net.Http.Json;

namespace Reaparr.BaseTests;

public static class HttpResponseMessageExtensions
{
    public static async Task<ResultDTO<T>> Deserialize<T>(this HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<ResultDTO<T>>(DefaultJsonSerializerOptions.ConfigStandard)
            ?? new ResultDTO<T>
            {
                IsSuccess = false,
                Errors = [],
                Successes = [],
                Value = default,
                StatusCode = 0,
            };

        result.Successes = result
            .Successes.Select(x => new SuccessDTO
            {
                Message = x.Message,
                Metadata = x.Metadata.ToTypedResultMetaData(),
            })
            .ToList();

        result.Errors = result
            .Errors.Select(x => new ErrorDTO
            {
                Reasons = x.Reasons,
                Message = x.Message,
                Metadata = x.Metadata.ToTypedResultMetaData(),
            })
            .ToList();

        return result;
    }

    /// <summary>
    /// Deserializing to Dictionary(string, object) will make the value of the Dictionary a JsonElement
    /// so we want to specify the type explicitly
    /// </summary>
    /// <param name="dict"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    private static Dictionary<string, object> ToTypedResultMetaData(this Dictionary<string, object> dict)
    {
        foreach (var keyValuePair in dict)
        {
            var value = keyValuePair.Value;
            if (value is JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Undefined:
                        break;
                    case JsonValueKind.Object:
                        value = element.GetRawText();
                        break;
                    case JsonValueKind.Array:
                        break;
                    case JsonValueKind.String:
                        value = element.GetString();
                        break;
                    case JsonValueKind.Number:
                        value = element.GetInt32();
                        break;
                    case JsonValueKind.True:
                        value = true;
                        break;
                    case JsonValueKind.False:
                        value = false;
                        break;
                    case JsonValueKind.Null:
                        value = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (value is not null)
                dict[keyValuePair.Key] = value;
        }

        return dict;
    }
}
