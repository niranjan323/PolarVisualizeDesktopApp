using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolarVisualizeDesktopApp.Services;

/// <summary>
/// Parameters for loading polar data
/// </summary>
public class PolarLoadParameters
{
    public string Draft { get; set; } = "scantling"; // scantling, design, intermediate
    public double GM { get; set; } = 1.5;
    public double Hs { get; set; } = 5.5;
    public double Tz { get; set; } = 7.5;
    public double DraftAftPeak { get; set; }
    public double DraftForePeak { get; set; }
}

/// <summary>
/// Result of polar data loading operation
/// </summary>
public class PolarLoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ResponseMatrix? Data { get; set; }
    public double FittedGM { get; set; }
    public double FittedHs { get; set; }
    public double FittedTz { get; set; }
    public string? FilePath { get; set; }
}

/// <summary>
/// Service for locating and loading polar response data files
/// </summary>
public class PolarService
{
    private readonly string _dataRootPath;

    public PolarService(IConfiguration configuration)
    {
        _dataRootPath = configuration["PolarData:RootPath"]
            ?? @"C:\PolarData";
    }

    /// <summary>
    /// Determines the appropriate draft category based on aft and fore peak drafts
    /// </summary>
    public string DetermineDraft(double draftAftPeak, double draftForePeak,
        double scantlingDraft, double designDraft, double intermediateDraft)
    {
        double meanDraft = 0.5 * (draftAftPeak + draftForePeak);

        if (meanDraft > 0.5 * (scantlingDraft + designDraft))
            return "scantling";
        else if (meanDraft > 0.5 * (designDraft + intermediateDraft))
            return "design";
        else
            return "intermediate";
    }

    /// <summary>
    /// Loads polar response data based on specified parameters
    /// </summary>
    public PolarLoadResult LoadPolarData(PolarLoadParameters parameters)
    {
        var result = new PolarLoadResult();

        try
        {
            // Round parameters to nearest available values
            result.FittedGM = Math.Round(parameters.GM * 2) / 2.0;  // Nearest 0.5
            result.FittedHs = Math.Round(parameters.Hs * 2) / 2.0;  // Nearest 0.5
            result.FittedTz = Math.Round(parameters.Tz * 2) / 2.0;  // Nearest 0.5

            // Build file path
            string fileName = $"MAXROLL_H{result.FittedHs:F1}_T{result.FittedTz:F1}.bpolar";
            string filePath = Path.Combine(
                _dataRootPath,
                parameters.Draft,
                $"GM={result.FittedGM:F1}m",
                "bin",
                fileName
            );

            result.FilePath = filePath;

            // Check if file exists
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Data file not found: {filePath}";
                return result;
            }

            // Read the file
            result.Data = PolarFileReader.ReadPolarFile(filePath);
            result.Success = true;

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error loading polar data: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Gets the image file path for a given set of parameters
    /// </summary>
    public string GetImageFilePath(PolarLoadParameters parameters)
    {
        double fittedGM = Math.Round(parameters.GM * 2) / 2.0;
        double fittedHs = Math.Round(parameters.Hs * 2) / 2.0;
        double fittedTz = Math.Round(parameters.Tz * 2) / 2.0;

        string fileName = $"POLAR_ROLL_H{fittedHs:F1}_T{fittedTz:F1}_polarplot.gif";
        return Path.Combine(
            _dataRootPath,
            parameters.Draft,
            $"GM={fittedGM:F1}m",
            "plots",
            fileName
        );
    }

    /// <summary>
    /// Gets available GM values for a specific draft
    /// </summary>
    public double[] GetAvailableGMValues(string draft)
    {
        try
        {
            string draftPath = Path.Combine(_dataRootPath, draft);
            if (!Directory.Exists(draftPath))
                return Array.Empty<double>();

            var gmDirs = Directory.GetDirectories(draftPath, "GM=*m")
                .Select(dir => {
                    string dirName = Path.GetFileName(dir);
                    string gmValue = dirName.Replace("GM=", "").Replace("m", "");
                    if (double.TryParse(gmValue, out double gm))
                        return (exists: true, value: gm);
                    return (exists: false, value: 0.0);
                })
                .Where(x => x.exists)
                .Select(x => x.value)
                .OrderBy(x => x)
                .ToArray();

            return gmDirs;
        }
        catch
        {
            return Array.Empty<double>();
        }
    }

    /// <summary>
    /// Validates if the data root path exists
    /// </summary>
    public bool ValidateDataPath()
    {
        return Directory.Exists(_dataRootPath);
    }

    /// <summary>
    /// Gets the configured data root path
    /// </summary>
    public string GetDataRootPath() => _dataRootPath;
}
