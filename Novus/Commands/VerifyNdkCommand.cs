using Novus.Tools;

namespace Novus.Commands;

public static class VerifyNdkCommand
{
    public static int Run(VerifyNdkOptions options)
    {
        try
        {
            var sourceStd = Path.Combine(Directory.GetCurrentDirectory(), "Novus", "std");
            var std = Directory.Exists(sourceStd) ? sourceStd : PathUtility.FindStdLibPath() ?? sourceStd;
            var raw = Path.GetFullPath(options.RawPath ?? Path.Combine(std, "amiga", "raw"));
            var manifestPath = Path.GetFullPath(options.ManifestPath ?? Path.Combine(raw, "ndk_coverage.json"));
            var ndk = options.NdkPath;
            if (string.IsNullOrWhiteSpace(ndk) && !options.Update)
            {
                var configured = UserConfig.ResolveNdkPath();
                if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured)) ndk = configured;
            }

            if (options.Update)
            {
                ndk = UserConfig.RequireNdkPath(ndk);
                var generated = NdkCoverage.Generate(ndk, raw);
                NdkCoverage.Write(generated, manifestPath);
                Console.WriteLine($"wrote {manifestPath}");
            }

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("NDK coverage manifest not found; run verify-ndk --update --ndk-path <NDK3.9>", manifestPath);

            var manifest = NdkCoverage.Read(manifestPath);
            var errors = NdkCoverage.Verify(manifest, raw, ndk);
            if (errors.Count > 0)
            {
                foreach (var error in errors) Console.Error.WriteLine($"error: {error}");
                Console.Error.WriteLine($"NDK coverage FAILED ({errors.Count} errors)");
                return Program.EXIT_COMPILE_ERROR;
            }

            Console.WriteLine($"NDK coverage verified: {manifest.Summary["symbols_total"]} symbols, {manifest.Summary["interfaces_total"]} interfaces");
            Console.WriteLine($"baseline: {manifest.Baseline.Name} [{manifest.Baseline.InputSha256[..12]}]");
            return Program.EXIT_SUCCESS;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return Program.EXIT_USAGE;
        }
    }
}
