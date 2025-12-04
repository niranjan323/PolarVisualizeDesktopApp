using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization; // ✅ ADD THIS
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PolarVisualizeDesktopApp.Services;

// Models - NO CHANGES
public class VesselInfo
{
    public string IMO { get; set; } = "";
    public string Name { get; set; } = "";
}

public class RepresentativeDraft
{
    public double Ts { get; set; }
    public double Td { get; set; }
    public double Ti { get; set; }
}

public class ParameterBound
{
    public double GM_lower { get; set; }
    public double GM_upper { get; set; }
    public double Hs_lower { get; set; }
    public double Hs_upper { get; set; }
    public double Tz_lower { get; set; }
    public double Tz_upper { get; set; }
}

public class PolarLoadParameters
{
    public string Draft { get; set; } = "scantling";
    public double GM { get; set; } = 1.5;
    public double Hs { get; set; } = 5.5;
    public double Tz { get; set; } = 7.5;
    public double DraftAftPeak { get; set; }
    public double DraftForePeak { get; set; }
}

public class PolarLoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ResponseMatrix? Data { get; set; }
    public double FittedGM { get; set; }
    public double FittedHs { get; set; }
    public double FittedTz { get; set; }
    public string? FilePath { get; set; }
    public string? ImageFilePath { get; set; }
}

public class ControlFileData
{
    public VesselInfo VesselInfo { get; set; } = new();
    public RepresentativeDraft RepresentativeDraft { get; set; } = new();
    public ParameterBound ParameterBounds { get; set; } = new();
}

public class PolarService
{
    private string _dataRootPath;
    private ControlFileData? _controlFileData;

    public PolarService(IConfiguration configuration)
    {
        _dataRootPath = "PolarData";
    }

    public async Task<bool> LoadControlFileAsync(string fileName = "proll.ctl")
    {
        try
        {
            string resourcePath = $"{_dataRootPath}/{fileName}";

            using var stream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
            using var reader = new StreamReader(stream);

            var content = await reader.ReadToEndAsync();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            _controlFileData = new ControlFileData();

            var imoLine = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (imoLine.Length > 0)
            {
                _controlFileData.VesselInfo.IMO = imoLine[0];
                if (imoLine.Length > 1)
                {
                    _controlFileData.VesselInfo.Name = string.Join(" ", imoLine.Skip(1));
                }
            }

            var draftLine = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (draftLine.Length >= 3)
            {
                _controlFileData.RepresentativeDraft.Ts = double.Parse(draftLine[0]);
                _controlFileData.RepresentativeDraft.Td = double.Parse(draftLine[1]);
                _controlFileData.RepresentativeDraft.Ti = double.Parse(draftLine[2]);
            }

            UpdateParameterBounds();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading control file: {ex.Message}");
            return false;
        }
    }

    public VesselInfo? GetVesselInfo()
    {
        return _controlFileData?.VesselInfo;
    }

    public RepresentativeDraft? GetRepresentativeDrafts()
    {
        return _controlFileData?.RepresentativeDraft;
    }

    private void UpdateParameterBounds()
    {
        if (_controlFileData == null) return;

        var bounds = _controlFileData.ParameterBounds;

        bounds.GM_lower = 1.0;
        bounds.GM_upper = 2.0;
        bounds.Hs_lower = 3.0;
        bounds.Hs_upper = 12.0;
        bounds.Tz_lower = 5.0;
        bounds.Tz_upper = 18.0;
    }

    public ParameterBound GetParameterBounds()
    {
        return _controlFileData?.ParameterBounds ?? new ParameterBound
        {
            GM_lower = 0.5,
            GM_upper = 5.0,
            Hs_lower = 3.0,
            Hs_upper = 12.0,
            Tz_lower = 5.0,
            Tz_upper = 18.0
        };
    }

    public string DetermineDraft(double draftAftPeak, double draftForePeak)
    {
        double meanDraft = 0.5 * (draftAftPeak + draftForePeak);

        if (_controlFileData?.RepresentativeDraft != null)
        {
            var rd = _controlFileData.RepresentativeDraft;

            if (meanDraft > 0.5 * (rd.Ts + rd.Td))
                return "scantling";
            else if (meanDraft > 0.5 * (rd.Td + rd.Ti))
                return "design";
            else
                return "intermediate";
        }

        if (meanDraft > 8.0)
            return "scantling";
        else if (meanDraft > 6.0)
            return "design";
        else
            return "intermediate";
    }

    // ✅ FIXED VERSION with culture-invariant formatting
    public async Task<PolarLoadResult> LoadPolarDataAsync(PolarLoadParameters parameters)
    {
        var result = new PolarLoadResult();

        try
        {
            if (parameters.DraftAftPeak > 0 && parameters.DraftForePeak > 0)
            {
                parameters.Draft = DetermineDraft(parameters.DraftAftPeak, parameters.DraftForePeak);
            }

            // Round parameters
            result.FittedGM = Math.Round(parameters.GM * 2) / 2.0;
            result.FittedHs = Math.Round(parameters.Hs * 2) / 2.0;
            result.FittedTz = Math.Round(parameters.Tz * 2) / 2.0;

            // ✅ FIXED: Use CultureInfo.InvariantCulture to ensure dot separator
            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "MAXROLL_H{0:F1}_T{1:F1}.bpolar",
                result.FittedHs,
                result.FittedTz
            );

            string relativePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/GM={1:F1}m/bin/{2}",
                parameters.Draft,
                result.FittedGM,
                fileName
            );

            result.FilePath = relativePath;

            string imageFileName = string.Format(
                CultureInfo.InvariantCulture,
                "POLAR_ROLL_H{0:F1}_T{1:F1}_polarplot.gif",
                result.FittedHs,
                result.FittedTz
            );

            string imagePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/GM={1:F1}m/plots/{2}",
                parameters.Draft,
                result.FittedGM,
                imageFileName
            );

            result.ImageFilePath = imagePath;

            // Debug output
            Console.WriteLine($"[PolarService] Draft: {parameters.Draft}");
            Console.WriteLine($"[PolarService] FittedGM: {result.FittedGM}");
            Console.WriteLine($"[PolarService] FittedHs: {result.FittedHs}");
            Console.WriteLine($"[PolarService] FittedTz: {result.FittedTz}");
            Console.WriteLine($"[PolarService] Looking for: {relativePath}");

            byte[]? fileData = await ReadEmbeddedFileAsync(relativePath);

            if (fileData == null || fileData.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = $"Data file not found: {fileName}\nPath: {relativePath}";
                return result;
            }

            result.Data = PolarFileReader.ReadPolarFileFromBytes(fileData, relativePath);
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error loading polar data: {ex.Message}\nStack: {ex.StackTrace}";
            return result;
        }
    }

    private async Task<byte[]?> ReadEmbeddedFileAsync(string relativePath)
    {
        try
        {
            string resourcePath = $"{_dataRootPath}/{relativePath}";

            Console.WriteLine($"[PolarService] Attempting to read: {resourcePath}");

            using var stream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);

            byte[] data = memoryStream.ToArray();
            Console.WriteLine($"[PolarService] Successfully read {data.Length} bytes");

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PolarService] Error reading '{relativePath}': {ex.Message}");
            return null;
        }
    }

    public async Task<string?> GetImageFilePathAsync(PolarLoadParameters parameters)
    {
        double fittedGM = Math.Round(parameters.GM * 2) / 2.0;
        double fittedHs = Math.Round(parameters.Hs * 2) / 2.0;
        double fittedTz = Math.Round(parameters.Tz * 2) / 2.0;

        string fileName = string.Format(
            CultureInfo.InvariantCulture,
            "POLAR_ROLL_H{0:F1}_T{1:F1}_polarplot.gif",
            fittedHs,
            fittedTz
        );

        string relativePath = string.Format(
            CultureInfo.InvariantCulture,
            "{0}/GM={1:F1}m/plots/{2}",
            parameters.Draft,
            fittedGM,
            fileName
        );

        try
        {
            string resourcePath = $"{_dataRootPath}/{relativePath}";
            using var stream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
            return relativePath;
        }
        catch
        {
            return null;
        }
    }

    public double[] GetAvailableGMValues(string draft)
    {
        return new double[] { 1.0, 1.5, 2.0 };
    }

    public bool ValidateDataPath()
    {
        return true;
    }

    public void SetDataRootPath(string path)
    {
        Console.WriteLine("[PolarService] SetDataRootPath ignored (using embedded resources)");
    }

    public string GetDataRootPath() => _dataRootPath;

    public bool IsControlFileLoaded() => _controlFileData != null;

    public double[] GetAvailableHsValues(string draft, double gm)
    {
        return new double[]
        {
            3.0, 3.5, 4.0, 4.5, 5.0, 5.5, 6.0, 6.5, 7.0, 7.5,
            8.0, 8.5, 9.0, 9.5, 10.0, 10.5, 11.0, 11.5, 12.0
        };
    }

    public double[] GetAvailableTzValues(string draft, double gm, double hs)
    {
        return new double[]
        {
            5.0, 5.5, 6.0, 6.5, 7.0, 7.5, 8.0, 8.5, 9.0, 9.5, 10.0,
            10.5, 11.0, 11.5, 12.0, 12.5, 13.0, 13.5, 14.0, 14.5,
            15.0, 15.5, 16.0, 16.5, 17.0, 17.5, 18.0
        };
    }
 
}