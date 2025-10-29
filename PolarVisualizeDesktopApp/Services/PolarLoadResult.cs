using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolarVisualizeDesktopApp.Services;


public class PolarLoadParameters
{
    public string Draft { get; set; } = "scantling"; // scantling, design, intermediate
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
}


public class PolarService
{
    private readonly string _dataRootPath;

    public PolarService(IConfiguration configuration)
    {
        _dataRootPath = configuration["PolarData:RootPath"]
            ?? @"C:\PolarData";
    }


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


    public bool ValidateDataPath()
    {
        return Directory.Exists(_dataRootPath);
    }

    public string GetDataRootPath() => _dataRootPath;
}
