using AvalNeroSercher.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AvalNeroSercher.Services;

public class ModelManager
{
    public string ModelsDirectory { get; }

    public ModelManager(string? modelsDirectory = null)
    {
        ModelsDirectory = modelsDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Ml_Model");

        Directory.CreateDirectory(ModelsDirectory);
    }

    public IReadOnlyList<ModelInfo> RefreshModels(string? loadedModelPath = null)
    {
        var models = Directory
            .EnumerateFiles(ModelsDirectory, "*.zip", SearchOption.TopDirectoryOnly)
            .Select(path => new ModelInfo
            {
                Name = Path.GetFileName(path),
                Path = path,
                IsLoaded = loadedModelPath != null &&
                           string.Equals(path, loadedModelPath, StringComparison.OrdinalIgnoreCase)
            })
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return models;
    }

    public ModelInfo? FindByName(string name)
    {
        var path = Path.Combine(ModelsDirectory, name);
        if (!File.Exists(path))
            return null;

        return new ModelInfo
        {
            Name = Path.GetFileName(path),
            Path = path
        };
    }
}
