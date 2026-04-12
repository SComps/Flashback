Imports System.IO
Imports System.Linq

Public Class SecurityUtils
    Public Shared Function SanitizeFilename(input As String) As String
        If String.IsNullOrWhiteSpace(input) Then Return "Unknown"
        
        Dim filename = Path.GetFileName(input)
        Dim invalidChars = Path.GetInvalidFileNameChars().Concat({Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar}).Distinct().ToArray()
        Dim clean = New String(filename.Where(Function(c) Not invalidChars.Contains(c)).ToArray())
        
        clean = clean.Trim("."c)
        If String.IsNullOrWhiteSpace(clean) Then Return "Unknown"
        Return clean
    End Function
End Class
