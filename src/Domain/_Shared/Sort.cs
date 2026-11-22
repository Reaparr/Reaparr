namespace Reaparr.Domain;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct Sort(SortType Field, SortDirection Direction);
