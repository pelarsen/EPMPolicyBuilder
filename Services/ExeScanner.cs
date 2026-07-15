using EPMPolicyBuilder.Models;
using System.Diagnostics;
using System.Security.Cryptography;

namespace EPMPolicyBuilder.Services;

public class ExeScanner
{
    /// <summary>
    /// Scans a directory tree for .exe files.
    /// </summary>
    /// <param name="directory">Root directory to scan.</param>
    /// <param name="progress">Reports (foundCount, currentFile, skippedFolderCount).</param>
    /// <param name="skipInaccessible">When true, silently skips folders the current user cannot read.</param>
    /// <param name="cancellationToken">Allows the caller to cancel the scan.</param>
    /// <returns>Scanned file list and count of skipped inaccessible folders.</returns>
    public async Task<(List<ExeFileInfo> Files, int SkippedFolders)> ScanDirectoryAsync(
        string directory,
        IProgress<(int count, string currentFile, int skippedFolders)>? progress,
        bool skipInaccessible,
        CancellationToken cancellationToken)
    {
        var results = new List<ExeFileInfo>();
        int count = 0;
        int skipped = 0;

        await Task.Run(() =>
        {
            WalkDirectory(directory, results, ref count, ref skipped, progress, skipInaccessible, cancellationToken);
        }, cancellationToken);

        return (results, skipped);
    }

    private static void WalkDirectory(
        string directory,
        List<ExeFileInfo> results,
        ref int count,
        ref int skipped,
        IProgress<(int count, string currentFile, int skippedFolders)>? progress,
        bool skipInaccessible,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Enumerate .exe files in this directory
        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);
        }
        catch (UnauthorizedAccessException)
        {
            if (!skipInaccessible) throw;
            skipped++;
            progress?.Report((count, directory, skipped));
            return;
        }
        catch (IOException)
        {
            if (!skipInaccessible) throw;
            skipped++;
            progress?.Report((count, directory, skipped));
            return;
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            count++;
            progress?.Report((count, file, skipped));

            try
            {
                var info = ReadExeInfo(file);
                if (info != null)
                    results.Add(info);
            }
            catch { /* skip unreadable files */ }
        }

        // Recurse into subdirectories
        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(directory);
        }
        catch (UnauthorizedAccessException)
        {
            if (!skipInaccessible) throw;
            skipped++;
            progress?.Report((count, directory, skipped));
            return;
        }
        catch (IOException)
        {
            if (!skipInaccessible) throw;
            skipped++;
            progress?.Report((count, directory, skipped));
            return;
        }

        foreach (var sub in subDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WalkDirectory(sub, results, ref count, ref skipped, progress, skipInaccessible, cancellationToken);
        }
    }

    /// <summary>Scans a single file and returns its metadata, or null if the file cannot be read.</summary>
    public ExeFileInfo? ScanSingleFile(string filePath) => ReadExeInfo(filePath);

    private static ExeFileInfo? ReadExeInfo(string filePath)
    {
        try
        {
            var fvi = FileVersionInfo.GetVersionInfo(filePath);
            var (isSigned, subject, issuer, thumbprint, serialNumber, publisherName) =
                CertificateReader.ReadCertificate(filePath);

            string? hash = null;
            try
            {
                var fi = new FileInfo(filePath);
                if (fi.Length < 500 * 1024 * 1024)
                {
                    using var stream = File.OpenRead(filePath);
                    using var sha256 = SHA256.Create();
                    hash = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
                }
            }
            catch { }

            return new ExeFileInfo
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                InternalName = fvi.InternalName ?? "",
                FileDescription = fvi.FileDescription ?? "",
                ProductName = fvi.ProductName ?? "",
                FileVersion = fvi.FileVersion ?? "",
                ProductVersion = fvi.ProductVersion ?? "",
                CompanyName = fvi.CompanyName ?? "",
                FileHash = hash ?? "",
                IsSigned = isSigned,
                CertificateSubject = subject,
                CertificateIssuer = issuer,
                CertificateThumbprint = thumbprint,
                CertificateSerialNumber = serialNumber,
                PublisherName = publisherName,
            };
        }
        catch
        {
            return null;
        }
    }
}
