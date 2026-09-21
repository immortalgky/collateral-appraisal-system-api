using Microsoft.AspNetCore.Hosting;
using Shared.Configurations;

namespace Document.Services;

/// <summary>
/// Where documents live. One answer, shared by everything that writes there, so a staging folder
/// and the folder a finished document is moved into can never end up on different shares — which
/// would turn the move at the end of a chunked upload from a rename into a copy of the whole file.
/// </summary>
internal static class DocumentStorage
{
    public static string BasePath(FileStorageConfiguration configuration, IWebHostEnvironment environment) =>
        configuration.Mode == StorageMode.Nas && !string.IsNullOrEmpty(configuration.NasBasePath)
            ? configuration.NasBasePath
            : environment.WebRootPath;
}
