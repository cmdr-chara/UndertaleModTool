using Microsoft.UI.Xaml.Controls;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace UndertaleModTool_WinUI;

public sealed partial class MainPage
{
    private const string VariableChunkCategoryLabel = "Variable chunk";
    private bool _parityFeaturesInitialized;
    private bool _isInjectingParityCategory;
    private UndertaleData? _parityExtensionData;
    private int _knownExtensionCount;

    internal void InitializeParityFeatures()
    {
        if (_parityFeaturesInitialized)
            return;

        _parityFeaturesInitialized = true;
        CategoryList.RegisterPropertyChangedCallback(
            ItemsControl.ItemsSourceProperty,
            (_, _) => RefreshParityFeatures());
        RefreshParityFeatures();
    }

    private void RefreshParityFeatures()
    {
        ApplyNewExtensionDefaults();
        EnsureVariableChunkCategory();
    }

    private void ApplyNewExtensionDefaults()
    {
        UndertaleData? data = _data;
        if (data?.Extensions is null)
        {
            _parityExtensionData = data;
            _knownExtensionCount = 0;
            return;
        }

        if (!ReferenceEquals(_parityExtensionData, data))
        {
            _parityExtensionData = data;
            _knownExtensionCount = data.Extensions.Count;
            return;
        }

        int currentCount = data.Extensions.Count;
        if (currentCount <= _knownExtensionCount)
        {
            _knownExtensionCount = currentCount;
            return;
        }

        bool changed = false;
        for (int index = _knownExtensionCount; index < currentCount; index++)
        {
            UndertaleExtension? extension = data.Extensions[index];
            if (extension is null)
                continue;

            if (extension.FolderName is null)
            {
                extension.FolderName = data.Strings.MakeString(string.Empty);
                changed = true;
            }

            if (extension.ClassName is null)
            {
                extension.ClassName = data.Strings.MakeString(string.Empty);
                changed = true;
            }

            if (extension.Version is null)
            {
                extension.Version = data.Strings.MakeString("1.0.0");
                changed = true;
            }
        }

        _knownExtensionCount = currentCount;
        if (changed)
            MarkDirty();
    }

    private void EnsureVariableChunkCategory()
    {
        if (_isInjectingParityCategory || _data?.FORM?.VARI is null)
            return;

        if (_categories.Any(category =>
                string.Equals(category.Label, VariableChunkCategoryLabel, StringComparison.Ordinal)))
        {
            return;
        }

        _isInjectingParityCategory = true;
        try
        {
            VariableChunkSettings settings = new(_data);
            ResourceItem item = BuildResourceItem(settings, 0);
            ResourceCategory category = new(
                VariableChunkCategoryLabel,
                Symbol.Setting,
                1,
                new[] { item },
                null);

            _categories = _categories.Concat(new[] { category }).ToArray();
            CategoryList.ItemsSource = _categories;
        }
        finally
        {
            _isInjectingParityCategory = false;
        }
    }

    private sealed class VariableChunkSettings(UndertaleData data)
    {
        public uint InstanceVarCount
        {
            get => data.VarCount1;
            set => data.VarCount1 = value;
        }

        public uint InstanceVarCountAgain
        {
            get => data.VarCount2;
            set => data.VarCount2 = value;
        }

        public uint MaxLocalVarCount
        {
            get => data.MaxLocalVarCount;
            set => data.MaxLocalVarCount = value;
        }

        public bool DifferentVarCounts
        {
            get => data.DifferentVarCounts;
            set => data.DifferentVarCounts = value;
        }

        public override string ToString() => "VARI chunk settings";
    }
}
