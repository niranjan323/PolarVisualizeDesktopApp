using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PolarVisualizeDesktopApp.Services;

// Models matching reference library
public class VesselInfo
{
    public string IMO { get; set; } = "";
    public string Name { get; set; } = "";
}

public class RepresentativeDraft
{
    public double Ts { get; set; }  // Scantling draft
    public double Td { get; set; }  // Design draft
    public double Ti { get; set; }  // Intermediate draft
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
        //_dataRootPath = configuration["PolarData:RootPath"]
        //    ?? @"C:\PolarData";
        _dataRootPath = "PolarData";
    }
    public async Task<bool> LoadControlFileAsync(string fileName = "proll.ctl")
    {
        try
        {
            // Read from embedded resource
            string resourcePath = $"{_dataRootPath}/{fileName}";

            using var stream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
            using var reader = new StreamReader(stream);

            // Read all content
            var content = await reader.ReadToEndAsync();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                return false;
            }

            _controlFileData = new ControlFileData();

            // Parse Line 0: IMO and vessel name
            var imoLine = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (imoLine.Length > 0)
            {
                _controlFileData.VesselInfo.IMO = imoLine[0];
                if (imoLine.Length > 1)
                {
                    _controlFileData.VesselInfo.Name = string.Join(" ", imoLine.Skip(1));
                }
            }

            // Parse Line 1: Representative drafts
            var draftLine = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (draftLine.Length >= 3)
            {
                _controlFileData.RepresentativeDraft.Ts = double.Parse(draftLine[0]);
                _controlFileData.RepresentativeDraft.Td = double.Parse(draftLine[1]);
                _controlFileData.RepresentativeDraft.Ti = double.Parse(draftLine[2]);
            }

            // Update parameter bounds
            UpdateParameterBounds();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading control file: {ex.Message}");
            return false;
        }
    }
    /// <summary>
    /// Load control file from project folder
    /// Control file format:
    /// Line 0: IMO_NUMBER [VESSEL_NAME]
    /// Line 1: SCANTLING_DRAFT DESIGN_DRAFT INTERMEDIATE_DRAFT
    /// </summary>
    public bool LoadControlFile(string controlFilePath)
    {
        try
        {
            if (!File.Exists(controlFilePath))
            {
                return false;
            }

            var lines = File.ReadAllLines(controlFilePath);
            if (lines.Length < 2)
            {
                return false;
            }

            _controlFileData = new ControlFileData();

            // Parse Line 0: IMO and vessel name
            var imoLine = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (imoLine.Length > 0)
            {
                _controlFileData.VesselInfo.IMO = imoLine[0];
                if (imoLine.Length > 1)
                {
                    _controlFileData.VesselInfo.Name = string.Join(" ", imoLine.Skip(1));
                }
            }

            // Parse Line 1: Representative drafts
            var draftLine = lines[1].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (draftLine.Length >= 3)
            {
                _controlFileData.RepresentativeDraft.Ts = double.Parse(draftLine[0]); // Scantling
                _controlFileData.RepresentativeDraft.Td = double.Parse(draftLine[1]); // Design
                _controlFileData.RepresentativeDraft.Ti = double.Parse(draftLine[2]); // Intermediate
            }

            // Update parameter bounds based on available data
            UpdateParameterBounds();

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading control file: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get vessel information from loaded control file
    /// </summary>
    public VesselInfo? GetVesselInfo()
    {
        return _controlFileData?.VesselInfo;
    }

    /// <summary>
    /// Get representative drafts from loaded control file
    /// </summary>
    public RepresentativeDraft? GetRepresentativeDrafts()
    {
        return _controlFileData?.RepresentativeDraft;
    }

    /// <summary>
    /// Update parameter bounds by scanning available data folders
    /// </summary>
    //private void UpdateParameterBounds()
    //{
    //    if (_controlFileData == null) return;

    //    var bounds = _controlFileData.ParameterBounds;

    //    // Scan for available GM values
    //    bounds.GM_lower = 1.0;
    //    bounds.GM_upper = 2.0;

    //    foreach (var draftFolder in new[] { "scantling", "design", "intermediate" })
    //    {
    //        var draftPath = Path.Combine(_dataRootPath, draftFolder);
    //        if (!Directory.Exists(draftPath)) continue;

    //        // Find max GM
    //        for (int i = 0; i < 20; i++)
    //        {
    //            double gm = 1.0 + 0.5 * i;
    //            string gmDir = Path.Combine(draftPath, $"GM={gm:F1}m");
    //            if (Directory.Exists(gmDir))
    //            {
    //                bounds.GM_upper = Math.Max(bounds.GM_upper, gm);
    //            }
    //        }

    //        // Find min GM
    //        for (int i = 0; i < 20; i++)
    //        {
    //            double gm = 9.0 - 0.5 * i;
    //            string gmDir = Path.Combine(draftPath, $"GM={gm:F1}m");
    //            if (Directory.Exists(gmDir))
    //            {
    //                bounds.GM_lower = Math.Min(bounds.GM_lower, gm);
    //            }
    //        }
    //    }

    //    // Set Hs bounds (can be refined by scanning files)
    //    bounds.Hs_lower = 3.0;
    //    bounds.Hs_upper = 12.0;

    //    // Set Tz bounds (can be refined by scanning files)
    //    bounds.Tz_lower = 5.0;
    //    bounds.Tz_upper = 18.0;
    //}
    private void UpdateParameterBounds()
    {
        if (_controlFileData == null) return;

        var bounds = _controlFileData.ParameterBounds;

        // For embedded resources, set fixed bounds based on your folders
        // Based on screenshot: GM=1.0m, GM=1.5m, GM=2.0m
        bounds.GM_lower = 1.0;
        bounds.GM_upper = 2.0;

        // Set Hs bounds
        bounds.Hs_lower = 3.0;
        bounds.Hs_upper = 12.0;

        // Set Tz bounds
        bounds.Tz_lower = 5.0;
        bounds.Tz_upper = 18.0;
    }

    /// <summary>
    /// Get parameter bounds for validation
    /// </summary>
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

    /// <summary>
    /// Determine draft category based on mean draft and representative drafts
    /// Matches reference library logic
    /// </summary>
    public string DetermineDraft(double draftAftPeak, double draftForePeak)
    {
        double meanDraft = 0.5 * (draftAftPeak + draftForePeak);

        // Use control file data if available
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

        // Default thresholds if no control file
        if (meanDraft > 8.0)
            return "scantling";
        else if (meanDraft > 6.0)
            return "design";
        else
            return "intermediate";
    }

    /// <summary>
    /// Load polar data matching reference library GetSingleResponseMatrix
    /// </summary>
    public PolarLoadResult LoadPolarData(PolarLoadParameters parameters)
    {
        var result = new PolarLoadResult();

        try
        {
            // Determine draft if not explicitly set
            if (parameters.DraftAftPeak > 0 && parameters.DraftForePeak > 0)
            {
                parameters.Draft = DetermineDraft(parameters.DraftAftPeak, parameters.DraftForePeak);
            }

            // Round parameters to nearest available values (matching reference logic)
            result.FittedGM = Math.Round(parameters.GM * 2) / 2.0;  // Nearest 0.5
            result.FittedHs = Math.Round(parameters.Hs * 2) / 2.0;  // Nearest 0.5
            result.FittedTz = Math.Round(parameters.Tz * 2) / 2.0;  // Nearest 0.5

            // Build file paths matching reference library format
            string fileName = $"MAXROLL_H{result.FittedHs:F1}_T{result.FittedTz:F1}.bpolar";
            string filePath = Path.Combine(
                _dataRootPath,
                parameters.Draft,
                $"GM={result.FittedGM:F1}m",
                "bin",
                fileName
            );

            // Build image file path
            string imageFileName = $"POLAR_ROLL_H{result.FittedHs:F1}_T{result.FittedTz:F1}_polarplot.gif";
            string imagePath = Path.Combine(
                _dataRootPath,
                parameters.Draft,
                $"GM={result.FittedGM:F1}m",
                "plots",
                imageFileName
            );

            result.FilePath = filePath;
            result.ImageFilePath = imagePath;

            // Check if file exists
            if (!File.Exists(filePath))
            {
                result.Success = false;
                result.ErrorMessage = $"Data file not found: {Path.GetFileName(filePath)}\nPath: {filePath}";
                return result;
            }

            // Read the binary file
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

    public async Task<PolarLoadResult> LoadPolarDataAsync(PolarLoadParameters parameters)
    {
        var result = new PolarLoadResult();

        try
        {
            // Determine draft if not explicitly set
            if (parameters.DraftAftPeak > 0 && parameters.DraftForePeak > 0)
            {
                parameters.Draft = DetermineDraft(parameters.DraftAftPeak, parameters.DraftForePeak);
            }

            // Round parameters to nearest available values
            result.FittedGM = Math.Round(parameters.GM * 2) / 2.0;
            result.FittedHs = Math.Round(parameters.Hs * 2) / 2.0;
            result.FittedTz = Math.Round(parameters.Tz * 2) / 2.0;

            // Build file path for embedded resource
            string fileName = $"MAXROLL_H{result.FittedHs:F1}_T{result.FittedTz:F1}.bpolar";
            string relativePath = $"{parameters.Draft}/GM={result.FittedGM:F1}m/bin/{fileName}";

            result.FilePath = relativePath;

            // Build image file path
            string imageFileName = $"POLAR_ROLL_H{result.FittedHs:F1}_T{result.FittedTz:F1}_polarplot.gif";
            string imagePath = $"{parameters.Draft}/GM={result.FittedGM:F1}m/plots/{imageFileName}";
            result.ImageFilePath = imagePath;

            // Read from embedded resource
            byte[]? fileData = await ReadEmbeddedFileAsync(relativePath);

            if (fileData == null || fileData.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = $"Data file not found: {fileName}\nPath: {relativePath}";
                return result;
            }

            // Read the binary file
            result.Data = PolarFileReader.ReadPolarFileFromBytes(fileData, relativePath);
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

    // ============================================
    // ✅ NEW METHOD: Read Embedded File
    // ============================================
    private async Task<byte[]?> ReadEmbeddedFileAsync(string relativePath)
    {
        try
        {
            // Full resource path: "PolarData/scantling/GM=1.5m/bin/MAXROLL_H5.5_T7.5.bpolar"
            string resourcePath = $"{_dataRootPath}/{relativePath}";

            Console.WriteLine($"[PolarService] Attempting to read: {resourcePath}");

            using var stream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
            using var memoryStream = new MemoryStream();

            await stream.CopyToAsync(memoryStream);

            byte[] data = memoryStream.ToArray();
            Console.WriteLine($"[PolarService] Successfully read {data.Length} bytes from {relativePath}");

            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PolarService] Error reading embedded file '{relativePath}': {ex.Message}");
            return null;
        }
    }
    /// <summary>
    /// Get path to associated image file if it exists
    /// </summary>
    public string? GetImageFilePath(PolarLoadParameters parameters)
    {
        double fittedGM = Math.Round(parameters.GM * 2) / 2.0;
        double fittedHs = Math.Round(parameters.Hs * 2) / 2.0;
        double fittedTz = Math.Round(parameters.Tz * 2) / 2.0;

        string fileName = $"POLAR_ROLL_H{fittedHs:F1}_T{fittedTz:F1}_polarplot.gif";
        string imagePath = Path.Combine(
            _dataRootPath,
            parameters.Draft,
            $"GM={fittedGM:F1}m",
            "plots",
            fileName
        );

        return File.Exists(imagePath) ? imagePath : null;
    }

    /// <summary>
    /// Get available GM values for a specific draft category
    /// </summary>
    public double[] GetAvailableGMValues(string draft)
    {
        // For embedded resources, return fixed values based on your folders
        // Based on screenshot: GM=1.0m, GM=1.5m, GM=2.0m
        return new double[] { 1.0, 1.5, 2.0 };
    }


    /// <summary>
    /// Validate if data root path exists
    /// </summary>
    public bool ValidateDataPath()
    {
        // For embedded resources, always return true
        return true;
    }

    /// <summary>
    /// Set custom data root path (e.g., from folder browser)
    /// </summary>
    public void SetDataRootPath(string path)
    {
        // Not applicable for embedded resources
        Console.WriteLine("[PolarService] SetDataRootPath called but ignored (using embedded resources)");
    }

    /// <summary>
    /// Get current data root path
    /// </summary>
    public string GetDataRootPath() => _dataRootPath;

    /// <summary>
    /// Check if control file is loaded
    /// </summary>
    public bool IsControlFileLoaded() => _controlFileData != null;

    /// <summary>
    /// Get available Hs values by scanning files
    /// </summary>
    public double[] GetAvailableHsValues(string draft, double gm)
    {
        // For embedded resources, return typical range
        // Adjust based on your actual files
        return new double[]
        {
            3.0, 3.5, 4.0, 4.5, 5.0, 5.5, 6.0, 6.5, 7.0, 7.5,
            8.0, 8.5, 9.0, 9.5, 10.0, 10.5, 11.0, 11.5, 12.0
        };
    }


    /// <summary>
    /// Get available Tz values by scanning files
    /// </summary>
    public double[] GetAvailableTzValues(string draft, double gm, double hs)
    {
        // For embedded resources, return typical range
        // Adjust based on your actual files
        return new double[]
        {
            5.0, 5.5, 6.0, 6.5, 7.0, 7.5, 8.0, 8.5, 9.0, 9.5, 10.0,
            10.5, 11.0, 11.5, 12.0, 12.5, 13.0, 13.5, 14.0, 14.5,
            15.0, 15.5, 16.0, 16.5, 17.0, 17.5, 18.0
        };
    }
    public async Task<string?> GetImageFilePathAsync(PolarLoadParameters parameters)
    {
        double fittedGM = Math.Round(parameters.GM * 2) / 2.0;
        double fittedHs = Math.Round(parameters.Hs * 2) / 2.0;
        double fittedTz = Math.Round(parameters.Tz * 2) / 2.0;

        string fileName = $"POLAR_ROLL_H{fittedHs:F1}_T{fittedTz:F1}_polarplot.gif";
        string relativePath = $"{parameters.Draft}/GM={fittedGM:F1}m/plots/{fileName}";

        try
        {
            string resourcePath = $"{_dataRootPath}/{relativePath}";
            using var stream = await FileSystem.OpenAppPackageFileAsync(resourcePath);
            return relativePath; // File exists
        }
        catch
        {
            return null; // File doesn't exist
        }
    }

}