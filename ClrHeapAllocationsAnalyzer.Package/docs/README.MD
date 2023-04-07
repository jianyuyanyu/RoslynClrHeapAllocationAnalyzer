Reflection IT - Clr Heap Allocation Analyzer
===================================

This project is a fork of the **archived** [RoslynClrHeapAllocationAnalyzer](https://github.com/microsoft/RoslynClrHeapAllocationAnalyzer) project. This allowed us to add new features for .NET6 and later to it. These newer targetframeworks have features which don't allocate objects any more which the original analyzer was still reporting. For example: in .NET6 string interpolation does not cause Boxing any more. This is now not reported any more.