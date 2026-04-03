Dim pin, configPath, fso, f, content
pin = Session.Property("CustomActionData")
If Len(Trim(pin)) = 0 Then pin = "0000"

configPath = "C:\ProgramData\KidMonitor\appsettings.json"
Set fso = CreateObject("Scripting.FileSystemObject")

' Read existing content if present, otherwise start fresh
If fso.FileExists(configPath) Then
  Set f = fso.OpenTextFile(configPath, 1)
  content = f.ReadAll
  f.Close
  ' Replace or append Dashboard.Pin value using simple string injection
  ' If Dashboard section exists, replace pin value
  If InStr(content, """Dashboard""") > 0 Then
    ' Replace the Pin value using regex
    Dim re
    Set re = New RegExp
    re.Pattern = "(""Pin""\s*:\s*"")[^""]*("")"
    re.Global = True
    re.IgnoreCase = True
    content = re.Replace(content, "$1" & pin & "$2")
  Else
    ' Append Dashboard section before last }
    Dim lastBrace
    lastBrace = InStrRev(content, "}")
    If lastBrace > 0 Then
      content = Left(content, lastBrace - 1) & _
        ",""Dashboard"":{""Pin"":""" & pin & """,""Port"":5110}}"
    End If
  End If
Else
  content = "{""Dashboard"":{""Pin"":""" & pin & """,""Port"":5110}}"
End If

Set f = fso.OpenTextFile(configPath, 2, True)
f.Write content
f.Close
