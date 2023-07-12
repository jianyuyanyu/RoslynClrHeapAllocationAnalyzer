Reflection IT - Clr Heap Allocation Analyzer
===================================

This project is a fork of the **archived** [RoslynClrHeapAllocationAnalyzer](https://github.com/microsoft/RoslynClrHeapAllocationAnalyzer) project. This allowed us to add new features for .NET6 and later to it. These newer targetframeworks have features which don't allocate objects any more which the original analyzer was still reporting. For example: 
- .NET6 string interpolation does not cause Boxing any more. 
- C# 11.0 doesn't cause a Type Conversion Allocation when creating a delegate to a Static Member.

# NuGet packages

| Package | Version |
| ------ | ------ |
| ReflectionIT.ClrHeapAllocationAnalyzer | [![NuGet](https://img.shields.io/nuget/v/ReflectionIT.ClrHeapAllocationAnalyzer)](https://www.nuget.org/packages/ReflectionIT.ClrHeapAllocationAnalyzer) |         

Quick Video: https://www.youtube.com/watch?v=Tw-wgT-cXYU&hd=1

Roslyn based C# heap allocation diagnostic analyzer that can detect explicit and many implicit allocations like boxing, display classes a.k.a closures, implicit delegate creations, etc.

You can find also install the Visual Studio Extension from the Visual Studio Marketplace, https://marketplace.visualstudio.com/items?itemName=FonsSonnemans.ClrHeapAllocationAnalyzer

![example](https://cloud.githubusercontent.com/assets/1930559/4606581/2a027d08-5225-11e4-8d4e-686c204a1267.png)

## Microsoft Open Source Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
