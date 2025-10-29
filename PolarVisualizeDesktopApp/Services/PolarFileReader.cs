using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PolarVisualizeDesktopApp.Services;

/// <summary>
/// Represents the response matrix data from a .bpolar file
/// </summary>
public class ResponseMatrix
{
    public int SpeedCount { get; set; }
    public int HeadingCount { get; set; }
    public double[] Speeds { get; set; } = Array.Empty<double>();
    public double[] Headings { get; set; } = Array.Empty<double>();
    public double[,] MaxRoll { get; set; } = new double[0, 0];
    public string SourceFile { get; set; } = string.Empty;
}

/// <summary>
/// Reads and parses .bpolar binary files
/// </summary>
public static class PolarFileReader
{
    /// <summary>
    /// Reads a .bpolar binary file and returns the response matrix
    /// </summary>
    /// <param name="filePath">Full path to the .bpolar file</param>
    /// <returns>ResponseMatrix containing speeds, headings, and roll data</returns>
    /// <exception cref="FileNotFoundException">Thrown when file doesn't exist</exception>
    /// <exception cref="InvalidDataException">Thrown when file format is invalid</exception>
    public static ResponseMatrix ReadPolarFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Polar file not found: {filePath}");
        }

        var response = new ResponseMatrix { SourceFile = filePath };

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            // Read header information
            string header1 = reader.ReadString();
            string header2 = reader.ReadString();

            // Read dimensions
            response.SpeedCount = reader.ReadInt32();
            response.HeadingCount = reader.ReadInt32();

            // Validate dimensions
            if (response.SpeedCount <= 0 || response.SpeedCount > 100)
                throw new InvalidDataException($"Invalid speed count: {response.SpeedCount}");

            if (response.HeadingCount <= 0 || response.HeadingCount > 360)
                throw new InvalidDataException($"Invalid heading count: {response.HeadingCount}");

            // Read status string
            string status = reader.ReadString();

            // Initialize arrays
            response.Speeds = new double[response.SpeedCount];
            response.Headings = new double[response.HeadingCount];
            response.MaxRoll = new double[response.SpeedCount, response.HeadingCount];

            // Read data matrix
            // Data is stored as [heading][speed] in the file
            for (int j = 0; j < response.HeadingCount; j++)
            {
                for (int i = 0; i < response.SpeedCount; i++)
                {
                    response.Speeds[i] = reader.ReadDouble();
                    response.Headings[j] = reader.ReadDouble();
                    response.MaxRoll[i, j] = reader.ReadDouble();
                }
            }

            return response;
        }
        catch (EndOfStreamException ex)
        {
            throw new InvalidDataException($"Unexpected end of file while reading: {filePath}", ex);
        }
        catch (Exception ex) when (ex is not FileNotFoundException && ex is not InvalidDataException)
        {
            throw new InvalidDataException($"Error reading polar file: {filePath}", ex);
        }
    }

    /// <summary>
    /// Attempts to read a polar file, returning null if it fails
    /// </summary>
    public static ResponseMatrix? TryReadPolarFile(string filePath)
    {
        try
        {
            return ReadPolarFile(filePath);
        }
        catch
        {
            return null;
        }
    }
}
