namespace Reaparr.Application;

public class GetFolderPathDirectoryRequest
{
    /// <summary>
    /// NOTE: This constructor is needed to make the query param optional in the front-end typescript-api generation.
    /// </summary>
    public GetFolderPathDirectoryRequest(string path = "")
    {
        Path = path;
    }

    [QueryParam, BindFrom("path")]
    public string Path { get; init; }
}

public class GetFolderPathDirectoryRequestValidator : Validator<GetFolderPathDirectoryRequest>
{
    public GetFolderPathDirectoryRequestValidator()
    {
        RuleFor(x => x.Path).NotNull();
    }
}

public class GetFolderPathDirectoryEndpoint : Endpoint<GetFolderPathDirectoryRequest, FileSystemDTO>
{
    private readonly ILogger _log;
    private readonly IDirectory _directory;
    private readonly IPath _path;
    private readonly IDiskProvider _diskProvider;

    public GetFolderPathDirectoryEndpoint(ILogger log, IDirectory directory, IPath path, IDiskProvider diskProvider)
    {
        _log = log.ForContext<GetFolderPathDirectoryEndpoint>();
        _directory = directory;
        _path = path;
        _diskProvider = diskProvider;
    }

    public override void Configure()
    {
        Get(ApiRoutes.FolderPathController + "/directory");

        Summary(x =>
        {
            x.Summary = "Get all the FolderPaths entities in the database";
        });
        Description(x =>
            x.Produces(StatusCodes.Status200OK, typeof(ResultDTO<FileSystemDTO>))
                .Produces(StatusCodes.Status400BadRequest, typeof(BaseResultDTO))
                .Produces(StatusCodes.Status500InternalServerError, typeof(BaseResultDTO))
        );
    }

    public override async Task HandleAsync(GetFolderPathDirectoryRequest req, CancellationToken ct)
    {
        _log.Here().DebugApiCall(HttpContext, req);
        var path = req.Path;

        var result = LookupContents(path, false, true);

        await Send.FluentResult(result, x => x.ToDTO(), ct);
    }

    private Result<FileSystemResult> LookupContents(
        string query,
        bool includeFiles,
        bool allowFoldersWithoutTrailingSlashes
    )
    {
        _log.Here().Debug("Looking up path: {Query}", query);

        if (!string.IsNullOrWhiteSpace(query) && _directory.Exists(query))
        {
            if (allowFoldersWithoutTrailingSlashes)
                return GetFileSystemResults(query, includeFiles);

            var lastSeparatorIndex = query.LastIndexOf(_path.DirectorySeparatorChar);
            var path = query.Substring(0, lastSeparatorIndex + 1);

            if (lastSeparatorIndex != -1)
                return GetFileSystemResults(path, includeFiles);
        }

        // If a path is invalid return root file system
        var defaultResult = new FileSystemResult
        {
            Directories = _diskProvider
                .GetAllMounts()
                .Select(d =>
                {
                    try
                    {
                        return new FileSystemModel
                        {
                            Type = FileSystemEntityType.Drive,
                            Name = _diskProvider.GetVolumeName(d),
                            Path = d.RootDirectory.FullName,
                            LastModified = d.RootDirectory.LastWriteTimeUtc,
                            Extension = string.Empty,
                            Size = d.TotalSize,
                            HasReadPermission = d.CanRead(),
                            HasWritePermission = d.CanWrite(),
                        };
                    }
                    catch (IOException ex) when (ex.Message.Contains("Stale file handle"))
                    {
                        _log.Here().Warning("Stale file handle detected for drive {DriveName}, skipping", d.Name);

                        return new FileSystemModel
                        {
                            Type = FileSystemEntityType.Drive,
                            Name = d.Name,
                            Path = d.Name, // Fallback to drive name only
                            LastModified = null, // Use null for unavailable timestamp
                            Extension = string.Empty,
                            Size = 0, // Use 0 as a fallback for unavailable size
                            HasReadPermission = false, // Assume no read permission for stale drives
                            HasWritePermission = false, // Assume no write permission for stale drives
                        };
                    }
                    catch (Exception ex)
                    {
                        _log.Here().Warning(ex, "Error accessing drive information for {DriveName}, skipping", d.Name);

                        return new FileSystemModel
                        {
                            Type = FileSystemEntityType.Drive,
                            Name = d.Name,
                            Path = d.Name, // Fallback to drive name only
                            LastModified = null, // Use null for unavailable timestamp
                            Extension = string.Empty,
                            Size = 0, // Use 0 as a fallback for unavailable size
                            HasReadPermission = false, // Assume no read permission for problematic drives
                            HasWritePermission = false, // Assume no write permission for problematic drives
                        };
                    }
                })
                .ToList(),
            Files = [],
            Parent = "",
            Current = null,
        };

        return Result.Ok(defaultResult);
    }

    private Result<FileSystemResult> GetFileSystemResults(string path, bool includeFiles)
    {
        try
        {
            var directoriesResult = _diskProvider.GetDirectories(path);
            if (directoriesResult.IsFailed)
                return directoriesResult.ToResult();

            if (includeFiles)
            {
                var filesResult = _diskProvider.GetFiles(path);
                if (filesResult.IsFailed)
                    return filesResult.ToResult();

                return Result.Ok(
                    new FileSystemResult
                    {
                        Parent = _diskProvider.GetParent(path),
                        Directories = directoriesResult.Value,
                        Files = filesResult.Value,
                        Current = new DirectoryInfo(path).ToModel(),
                    }
                );
            }

            return Result.Ok(
                new FileSystemResult
                {
                    Parent = _diskProvider.GetParent(path),
                    Directories = directoriesResult.Value,
                    Files = [],
                    Current = new DirectoryInfo(path).ToModel(),
                }
            );
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e)).LogError();
        }
    }
}
