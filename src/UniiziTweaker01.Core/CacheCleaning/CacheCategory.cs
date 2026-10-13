#nullable enable
using System;
using System.Collections.Generic;

namespace UniiziTweaker01.Core.CacheCleaning;

/// <summary>Well-known base folders. The Windows layer resolves them to real paths.</summary>
public enum KnownRoot { Temp, LocalAppData, ProgramData }

public enum CategoryRisk { Safe, Caution, NeedsExplicitConfirmation }

/// <summary>A folder to clean, relative to a known root. Segments may contain * or ? wildcards.</summary>
public sealed record RootSpec(KnownRoot Root, string RelativePath);

public sealed record CacheCategory(
    string Id,
    string Name,
    string Description,
    CategoryRisk Risk,
    bool SelectedByDefault,
    TimeSpan MinAge,
    IReadOnlyList<RootSpec> Roots,
    bool IsRecycleBin = false,
    string Note = "");
