using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Revia.Storage;

namespace Revia.Modules.Lending.Services;

public sealed class LendingPhotoStorage(IConfiguration configuration, IWebHostEnvironment environment)
    : DiskFileStorage(configuration, environment, "LendingPhotos:StoragePath", "lending-photos"), ILendingPhotoStorage;
