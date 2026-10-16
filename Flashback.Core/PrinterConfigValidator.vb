Imports System.IO
Imports System.Collections.Generic
Imports System.Linq

Public Class PrinterConfigValidator
        Public Class ValidationResult
            Public Property IsValid As Boolean = True
            Public ReadOnly Property Errors As New List(Of String)
            Public ReadOnly Property DirectoryConflicts As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        End Class

        ''' <summary>
        ''' Normalizes an output directory path into a canonical, absolute directory path.
        ''' Standardizes directory separators, resolves relative paths against baseDir,
        ''' and strips trailing separators for reliable path comparison.
        ''' </summary>
        Public Shared Function NormalizeDirectory(outDest As String, Optional baseDir As String = Nothing) As String
            If String.IsNullOrWhiteSpace(baseDir) Then
                baseDir = AppDomain.CurrentDomain.BaseDirectory
            End If

            Dim dest = If(String.IsNullOrWhiteSpace(outDest), "Output", outDest.Trim())
            dest = dest.Replace("/"c, Path.DirectorySeparatorChar).Replace("\"c, Path.DirectorySeparatorChar)

            Dim fullPath As String
            If Path.IsPathRooted(dest) Then
                fullPath = Path.GetFullPath(dest)
            Else
                fullPath = Path.GetFullPath(Path.Combine(baseDir, dest))
            End If

            fullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar)
            If fullPath.EndsWith(":") Then
                fullPath &= Path.DirectorySeparatorChar
            ElseIf String.IsNullOrEmpty(fullPath) Then
                fullPath = Path.DirectorySeparatorChar.ToString()
            End If

            Return fullPath
        End Function

        ''' <summary>
        ''' Validates lines from a devices.dat file to ensure that no two printers
        ''' share the same normalized output directory.
        ''' </summary>
        Public Shared Function ValidateLines(lines As IEnumerable(Of String), Optional baseDir As String = Nothing) As ValidationResult
            Dim result As New ValidationResult()
            If lines Is Nothing Then Return result

            Dim dirToPrinters As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

            For Each line In lines
                If String.IsNullOrWhiteSpace(line) Then Continue For
                Dim p = line.Split("||", StringSplitOptions.TrimEntries)
                If p.Length < 10 Then Continue For

                Dim devName = If(String.IsNullOrWhiteSpace(p(0)), "UnnamedPrinter", p(0))
                Dim rawOutDest = p(9)
                Dim normalizedDir = NormalizeDirectory(rawOutDest, baseDir)

                If Not dirToPrinters.ContainsKey(normalizedDir) Then
                    dirToPrinters(normalizedDir) = New List(Of String)()
                End If
                dirToPrinters(normalizedDir).Add(devName)
            Next

            For Each kvp In dirToPrinters
                If kvp.Value.Count > 1 Then
                    result.IsValid = False
                    result.DirectoryConflicts(kvp.Key) = kvp.Value
                    Dim printerNames = String.Join("', '", kvp.Value)
                    result.Errors.Add($"Multiple printers ('{printerNames}') are configured to write output to the same directory: '{kvp.Key}'")
                End If
            Next

            Return result
        End Function

        ''' <summary>
        ''' Validates a devices.dat file to ensure no two printers share an output directory.
        ''' If the file does not exist, returns IsValid = True.
        ''' </summary>
        Public Shared Function ValidatePrinters(configFilePath As String, Optional baseDir As String = Nothing) As ValidationResult
            If Not File.Exists(configFilePath) Then
                Return New ValidationResult()
            End If

            Dim lines = File.ReadAllLines(configFilePath)
            Return ValidateLines(lines, baseDir)
        End Function

        ''' <summary>
        ''' Validates an in-memory collection of Devs objects before saving to ensure
        ''' no two printers share an output directory.
        ''' </summary>
        Public Shared Function ValidateDevices(devices As IEnumerable(Of Devs), Optional baseDir As String = Nothing) As ValidationResult
            If devices Is Nothing Then Return New ValidationResult()
            Return ValidateLines(devices.Select(Function(d) d.ToConfigLine()), baseDir)
        End Function
    End Class
