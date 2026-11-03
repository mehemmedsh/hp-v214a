# Qwen (yerel model, Ollama) ile on inceleme ve duzeltme kontrolu. Ayrinti: docs/05-HANDOFF.md -> 9.
# Bu dosya ASCII kalmali: Windows PowerShell 5.1 BOM'suz .ps1 dosyasini ANSI okur.
# Turkce metinler tools/dev/qwen/ altinda (UTF-8); betikteki Turkce harfler {hhhh} ile yazilir (TR).
#
# Kullanim (repo kokunde; Windows PowerShell 5.1 ve PowerShell 7):
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools/dev/qwen-review.ps1 -Check
#   ... -File tools/dev/qwen-review.ps1 -Task T003              staged fark; staged yoksa "T003: " commit'leri
#   ... -File tools/dev/qwen-review.ps1 -Task T003 -Mode fix    DUZELT listesi: .qwen/duzelt-T003.md (ya da -List)
#   ... -File tools/dev/qwen-review.ps1 -Task T311 -Commits d7b066d -Key tools/dev/qwen/kalibrasyon/T311-anahtar.txt
# Secenekler: -Commits a,b  -Staged  -List <dosya>  -Key <dosya>  -Name <ad>  -Context <token>
#             -Think true|false|low|medium|high  -DryRun (modeli cagirmaz)  -Quiet  -Model  -OllamaUrl  -OutDir
# Cikis kodu: 0 tamam, 1 kullanim/ayar hatasi, 2 Ollama'ya ulasilamadi ya da model yok, 3 cevap eksik/yarim.
[CmdletBinding()]
param(
    [string]$Task = '',
    [ValidateSet('review', 'fix')][string]$Mode = 'review',
    [string]$Commits = '',
    [switch]$Staged,
    [string]$List = '',
    [string]$Key = '',
    [string]$Name = '',
    [string]$Model = 'qwen3.8:27b',
    [int]$Context = 0,
    [string]$Think = 'true',
    [double]$CharsPerToken = 3.0,
    [int]$MinOutput = 12000,
    [string]$OllamaUrl = 'http://127.0.0.1:11434',
    [string]$OutDir = '',
    [switch]$DryRun,
    [switch]$Check,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$Utf8 = [System.Text.UTF8Encoding]::new($false)
try { [Console]::OutputEncoding = $Utf8 } catch { }
$OutputEncoding = $Utf8
Add-Type -AssemblyName System.Net.Http

# --- yardimcilar -------------------------------------------------------------

# Turkce ve ozel harfler {hhhh} (UTF-16 kodu) ile yazilir: TR 'hari{00e7}' -> 'hari' + c-cedilla.
function TR([string]$s) { return [regex]::Replace($s, '\{([0-9a-fA-F]{4})\}', { param($m) [string][char][Convert]::ToInt32($m.Groups[1].Value, 16) }) }
function Say([string]$m) { Write-Host $m }
function Warn([string]$m) { Write-Host ('UYARI: ' + $m) -ForegroundColor Yellow }
function Fail([string]$m, [int]$code = 1) { Write-Host ('HATA: ' + $m) -ForegroundColor Red; exit $code }
function Read-Text([string]$p) { return ([IO.File]::ReadAllText($p) -replace "`r`n", "`n") }
function Write-Text([string]$p, [string]$s) { [IO.File]::WriteAllText($p, $s, $Utf8) }
function Resolve-UserPath([string]$p) {
    if ([IO.Path]::IsPathRooted($p)) { return $p }
    return (Join-Path (Get-Location).Path $p)
}
function Est([string]$s) { return [int][Math]::Ceiling($s.Length / $CharsPerToken) }
function Get-Prop($o, [string]$n) {
    if ($null -eq $o) { return $null }
    $p = $o.PSObject.Properties[$n]
    if ($p) { return $p.Value }
    return $null
}
function Format-Sec([double]$s) { return ('{0} dk {1} sn' -f [int][Math]::Floor($s / 60), [int][Math]::Floor($s % 60)) }

$Root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$QwenDir = Join-Path $PSScriptRoot 'qwen'
if (-not $OutDir) { $OutDir = Join-Path $Root '.qwen' } else { $OutDir = Resolve-UserPath $OutDir }
[void][IO.Directory]::CreateDirectory($OutDir)
if ($Context -le 0) {
    $envCtx = 0
    if ([int]::TryParse([string]$env:OLLAMA_CONTEXT_LENGTH, [ref]$envCtx) -and $envCtx -ge 4096) { $Context = $envCtx } else { $Context = 32768 }
}

# --- dis komutlar (git, ollama, nvidia-smi): cikti UTF-8 okunur ---------------

function Quote-Arg([string]$a) {
    if ($a -eq '') { return '""' }
    if ($a -notmatch '[\s"]') { return $a }
    $q = $a -replace '(\\*)"', '$1$1\"'
    $q = $q -replace '(\\+)$', '$1$1'
    return '"' + $q + '"'
}
function Invoke-Native([string]$exe, [string[]]$argv) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exe
    $psi.Arguments = ((@($argv) | ForEach-Object { Quote-Arg $_ }) -join ' ')
    $psi.WorkingDirectory = $Root
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = $Utf8
    $psi.StandardErrorEncoding = $Utf8
    $p = New-Object System.Diagnostics.Process
    $p.StartInfo = $psi
    try { [void]$p.Start() } catch { return [pscustomobject]@{ Code = -1; Out = ''; Err = $_.Exception.Message } }
    $o = $p.StandardOutput.ReadToEndAsync()
    $e = $p.StandardError.ReadToEndAsync()
    $p.WaitForExit()
    return [pscustomobject]@{ Code = $p.ExitCode; Out = ($o.Result -replace "`r`n", "`n"); Err = $e.Result }
}
function Invoke-Git([string[]]$argv, [switch]$AllowFail) {
    $r = Invoke-Native 'git' (@('-c', 'core.quotepath=off', '-c', 'color.ui=never') + @($argv))
    if ($r.Code -ne 0 -and -not $AllowFail) { Fail ('git ' + (@($argv) -join ' ') + ': ' + ([string]$r.Err).Trim()) }
    return $r
}

# --- Ollama -------------------------------------------------------------------

$script:Http = $null
function Get-Http {
    if ($null -eq $script:Http) {
        $h = [System.Net.Http.HttpClientHandler]::new()
        $h.UseProxy = $false
        $c = [System.Net.Http.HttpClient]::new($h)
        $c.Timeout = [TimeSpan]::FromMinutes(120)
        $script:Http = $c
    }
    return $script:Http
}
function New-JsonContent($body) {
    $json = ConvertTo-Json -InputObject $body -Depth 8 -Compress
    $c = [System.Net.Http.ByteArrayContent]::new($Utf8.GetBytes($json))
    $c.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('application/json; charset=utf-8')
    return $c
}
function Invoke-Ollama([string]$path, $body) {
    $url = $OllamaUrl.TrimEnd('/') + $path
    try {
        if ($null -eq $body) { $resp = (Get-Http).GetAsync($url).GetAwaiter().GetResult() }
        else { $resp = (Get-Http).PostAsync($url, (New-JsonContent $body)).GetAwaiter().GetResult() }
    }
    catch { Fail ("Ollama'ya ulasilamadi (" + $OllamaUrl + '): ' + $_.Exception.GetBaseException().Message + '. Ollama uygulamasi acik mi?') 2 }
    $text = $Utf8.GetString($resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult())
    $data = $null
    if ($resp.IsSuccessStatusCode -and $text.Trim()) { $data = $text | ConvertFrom-Json }
    return [pscustomobject]@{ Ok = $resp.IsSuccessStatusCode; Status = [int]$resp.StatusCode; Text = $text; Data = $data }
}
function Get-ThinkValue([string]$t) {
    $v = $t.Trim().ToLowerInvariant()
    if ($v -eq 'true') { return $true }
    if ($v -eq 'false') { return $false }
    return $v
}
function Invoke-Chat([string]$prompt, [int]$numPredict) {
    $body = [ordered]@{
        model    = $Model
        messages = @([ordered]@{ role = 'user'; content = $prompt })
        stream   = $true
        think    = (Get-ThinkValue $Think)
        options  = [ordered]@{ num_ctx = $Context; num_predict = $numPredict }
    }
    $req = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Post, ($OllamaUrl.TrimEnd('/') + '/api/chat'))
    $req.Content = New-JsonContent $body
    $sw = [Diagnostics.Stopwatch]::StartNew()
    try { $resp = (Get-Http).SendAsync($req, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult() }
    catch { Fail ("Ollama'ya ulasilamadi (" + $OllamaUrl + '): ' + $_.Exception.GetBaseException().Message) 2 }
    if (-not $resp.IsSuccessStatusCode) {
        $t = $Utf8.GetString($resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult())
        Fail ('Ollama ' + [int]$resp.StatusCode + ': ' + $t) 3
    }
    $reader = [System.IO.StreamReader]::new($resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult(), $Utf8)
    $sbT = [System.Text.StringBuilder]::new()
    $sbC = [System.Text.StringBuilder]::new()
    $nT = 0; $nC = 0; $final = $null; $lastPrint = 0
    while ($true) {
        $line = $reader.ReadLine()
        if ($null -eq $line) { break }
        if (-not $line.Trim()) { continue }
        $o = $line | ConvertFrom-Json
        $err = Get-Prop $o 'error'
        if ($err) { Fail ('Ollama: ' + $err) 3 }
        $msg = Get-Prop $o 'message'
        $th = Get-Prop $msg 'thinking'
        if ($th) { [void]$sbT.Append($th); $nT++ }
        $ct = Get-Prop $msg 'content'
        if ($ct) { [void]$sbC.Append($ct); $nC++ }
        if (Get-Prop $o 'done') { $final = $o }
        $sec = [int]$sw.Elapsed.TotalSeconds
        if ($sec - $lastPrint -ge 20) { $lastPrint = $sec; Say ('  ... {0} sn | dusunme ~{1} token | cevap ~{2} token' -f $sec, $nT, $nC) }
    }
    $reader.Dispose()
    $sw.Stop()
    return [pscustomobject]@{ Thinking = $sbT.ToString(); Content = $sbC.ToString(); Final = $final; Seconds = $sw.Elapsed.TotalSeconds; NThink = $nT; NContent = $nC }
}

function Invoke-Check {
    Say ('Ollama: ' + $OllamaUrl + ' | model: ' + $Model + ' | baglam (num_ctx): ' + $Context)
    foreach ($n in @('OLLAMA_CONTEXT_LENGTH', 'OLLAMA_NUM_PARALLEL', 'OLLAMA_KV_CACHE_TYPE', 'OLLAMA_FLASH_ATTENTION', 'CUDA_VISIBLE_DEVICES')) {
        $v = [Environment]::GetEnvironmentVariable($n)
        if (-not $v) { $v = '(yok)' }
        Say ('  ' + $n + ' = ' + $v)
    }
    $ver = Invoke-Ollama '/api/version' $null
    if (-not $ver.Ok) { Fail ('/api/version: ' + $ver.Status) 2 }
    Say ('Ollama surumu: ' + (Get-Prop $ver.Data 'version'))
    $show = Invoke-Ollama '/api/show' ([ordered]@{ model = $Model })
    if (-not $show.Ok) { Fail ('Model bulunamadi: ' + $Model + " ('ollama list' ile adini kontrol et)") 2 }
    $d = Get-Prop $show.Data 'details'
    Say ('Model: ' + (Get-Prop $d 'parameter_size') + ', ' + (Get-Prop $d 'quantization_level') + ', aile ' + (Get-Prop $d 'family'))
    $caps = Get-Prop $show.Data 'capabilities'
    if ($caps) { Say ('Yetenekler: ' + (@($caps) -join ', ')) }
    $thk = Get-Prop $show.Data 'thinking'
    if ($thk) { Say ('Dusunme secenekleri (-Think): ' + (ConvertTo-Json -InputObject $thk -Compress -Depth 4)) }
    $mi = Get-Prop $show.Data 'model_info'
    if ($mi) { foreach ($p in $mi.PSObject.Properties) { if ($p.Name -like '*.context_length') { Say ('Modelin en buyuk baglami: ' + $p.Value) } } }

    $expected = TR '{00c7}{0131}{011f} d{00fc}{015f}t{00fc}; {00f6}l{00e7}{00fc} {015f}i{015f}e a{011f}a{00e7} {0130}zmir I{011f}d{0131}r'
    $prompt = (TR 'A{015f}a{011f}{0131}daki sat{0131}r{0131} aynen tekrar et; ba{015f}ka hi{00e7}bir {015f}ey yazma:') + "`n" + $expected
    Say ('Turkce karakter denemesi (model ' + $Context + ' baglamla yukleniyor; ilk seferde 10-60 sn surebilir)...')
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $chat = Invoke-Ollama '/api/chat' ([ordered]@{ model = $Model; messages = @([ordered]@{ role = 'user'; content = $prompt }); stream = $false; think = $false; options = [ordered]@{ num_ctx = $Context; num_predict = 200 } })
    $sw.Stop()
    if (-not $chat.Ok) { Fail ('Sohbet denemesi basarisiz (' + $chat.Status + '): ' + $chat.Text) 2 }
    $answer = [string](Get-Prop (Get-Prop $chat.Data 'message') 'content')
    if ($answer.IndexOf($expected, [StringComparison]::Ordinal) -ge 0) { Say ('UTF-8: TAMAM (' + [int]$sw.Elapsed.TotalSeconds + ' sn)') }
    else { Warn ('UTF-8: BOZUK ya da model farkli yazdi. Beklenen: ' + $expected + ' | Gelen: ' + $answer.Trim()) }
    $ps = Invoke-Native 'ollama' @('ps')
    if ($ps.Code -eq 0) { Say 'ollama ps:'; Say $ps.Out.TrimEnd() } else { Warn "'ollama ps' calismadi (ollama komutu PATH'te mi?)" }
    $gpu = Invoke-Native 'nvidia-smi' @('--query-gpu=index,name,memory.used,memory.total', '--format=csv,noheader')
    if ($gpu.Code -eq 0) { Say 'nvidia-smi (sira, ad, kullanilan, toplam):'; Say $gpu.Out.TrimEnd() } else { Warn "'nvidia-smi' calismadi" }
    Say 'Bakilacaklar: ollama ps -> CONTEXT = baglam ve PROCESSOR = 100% GPU; 3090 kullanilan bellek 23 GB altinda.'
    Say "Degilse OLLAMA_CONTEXT_LENGTH degerini 32768 yap, Ollama'yi yeniden baslat, tekrar dene (05-HANDOFF -> 0.5)."
}

# --- dokumanlar ---------------------------------------------------------------

$script:DocRev = 'HEAD'   # '' = index (staged)
function Get-Doc([string]$path) {
    $r = Invoke-Git @('show', ($script:DocRev + ':' + $path)) -AllowFail
    if ($r.Code -ne 0) { return $null }
    return $r.Out
}
function Get-Headings([string[]]$lines) {
    $res = @()
    $fence = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '^\s*(```|~~~)') { $fence = -not $fence; continue }
        if ($fence) { continue }
        if ($l -match '^(#{1,6})\s+(.+?)\s*$') { $res += [pscustomobject]@{ Index = $i; Level = $Matches[1].Length; Text = $Matches[2] } }
    }
    return $res
}
function Get-Sections([string]$text, [string[]]$names, [string]$label) {
    $lines = $text -split "`n"
    $hs = @(Get-Headings $lines)
    $ranges = @()
    foreach ($n in $names) {
        $hit = $false
        for ($k = 0; $k -lt $hs.Count; $k++) {
            if ($hs[$k].Text.StartsWith($n, [StringComparison]::OrdinalIgnoreCase)) {
                $end = $lines.Count - 1
                for ($j = $k + 1; $j -lt $hs.Count; $j++) { if ($hs[$j].Level -le $hs[$k].Level) { $end = $hs[$j].Index - 1; break } }
                $ranges += [pscustomobject]@{ S = $hs[$k].Index; E = $end }
                $hit = $true
                break
            }
        }
        if (-not $hit) { Warn ('Bolum bulunamadi: ' + $label + ' -> ' + $n) }
    }
    if ($ranges.Count -eq 0) { return '' }
    $sorted = @($ranges | Sort-Object S)
    $merged = @()
    $cs = $sorted[0].S; $ce = $sorted[0].E
    for ($i = 1; $i -lt $sorted.Count; $i++) {
        if ($sorted[$i].S -le $ce + 1) { if ($sorted[$i].E -gt $ce) { $ce = $sorted[$i].E } }
        else { $merged += [pscustomobject]@{ S = $cs; E = $ce }; $cs = $sorted[$i].S; $ce = $sorted[$i].E }
    }
    $merged += [pscustomobject]@{ S = $cs; E = $ce }
    $parts = @()
    foreach ($m in $merged) { $parts += (($lines[$m.S..$m.E]) -join "`n").TrimEnd() }
    return ($parts -join "`n`n")
}
function Get-TaskBlocks([string]$text, [string]$task) {
    $lines = $text -split "`n"
    $hs = @(Get-Headings $lines)
    $parts = @()
    for ($k = 0; $k -lt $hs.Count; $k++) {
        $s = $hs[$k].Index
        if ($k + 1 -lt $hs.Count) { $e = $hs[$k + 1].Index - 1 } else { $e = $lines.Count - 1 }
        $block = (($lines[$s..$e]) -join "`n").TrimEnd()
        if ($block -match ('\b' + [regex]::Escape($task) + '\b')) { $parts += $block }
    }
    return ($parts -join "`n`n")
}
# AGENTS.md -> Okuma haritasi: hangi dokumanin hangi bolumu bu goreve girer
function Get-MapPlan([string]$agentsText, [string]$mapHeading, [string]$task) {
    $sec = Get-Sections $agentsText @($mapHeading) 'okuma haritasi'
    $arrow = [string][char]0x2192
    $plan = [ordered]@{}
    foreach ($row in ($sec -split "`n")) {
        if ($row -notmatch '^\s*\|') { continue }
        $cells = @($row.Trim().Trim('|') -split '\|')
        if ($cells.Count -lt 2) { continue }
        $c1 = $cells[0].Trim(); $c2 = $cells[1].Trim()
        if ($c1 -match '^:?-{3,}' -or $c1 -eq 'Ne') { continue }
        $paths = @([regex]::Matches($c1, '`([^`]+)`') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -match '/|\.md$' })
        if ($paths.Count -eq 0) { continue }
        $nums = @([regex]::Matches($c2, 'T\d{3}') | ForEach-Object { $_.Value })
        if ($c2 -match (TR 'hari{00e7}')) { $inc = -not ($nums -contains $task) }           # "... harici"
        elseif ($nums.Count -gt 0) { $inc = ($nums -contains $task) }
        elseif ($c2 -match '^Her\b') { $inc = $true }                                  # "Her gorev", "Her kod gorevi"
        elseif ($c2 -match (TR 'ge{00e7}iyorsa')) { $inc = $true }                          # "Gorev numarasi geciyorsa"
        else { Warn ('Okuma haritasi satiri anlasilamadi: ' + $row.Trim()); continue }
        if (-not $inc) { continue }
        $taskMode = ($c1 -match (TR 'ge{00e7}ti{011f}i b{00f6}l{00fc}m'))                   # "gorev numarasinin gectigi bolumler"
        $names = @()
        $ai = $c1.IndexOf($arrow)
        if ($ai -ge 0) {
            $tail = $c1.Substring($ai + 1) -replace '\([^)]*\)', ''
            $tail = $tail -replace '^\s*sadece\s+', ''
            $tail = $tail -replace (TR '["{201c}{201d}]'), ''
            $names = @($tail.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        }
        foreach ($p in $paths) {
            if ($p -eq 'docs/04-FAZLAR.md') { continue }   # gorev satiri ayrica eklenir
            if ($taskMode) { $m = 'task' } elseif ($p.EndsWith('/')) { $m = 'dir' } elseif ($names.Count -gt 0) { $m = 'sections' } else { $m = 'whole' }
            if (-not $plan.Contains($p)) { $plan[$p] = [pscustomobject]@{ Mode = $m; Names = @() } }
            $e = $plan[$p]
            if ($m -eq 'whole') { $e.Mode = 'whole' }
            elseif ($m -eq 'sections') { if ($e.Mode -eq 'sections') { $e.Names = @($e.Names) + $names } }
            elseif ($e.Mode -ne 'whole') { $e.Mode = $m }
        }
    }
    return $plan
}
function Get-DirDocs([string]$dir) {
    if ($script:DocRev) { $r = Invoke-Git @('ls-tree', '-r', '--name-only', $script:DocRev, '--', $dir) -AllowFail }
    else { $r = Invoke-Git @('ls-files', '--', $dir) -AllowFail }
    $parts = @()
    foreach ($f in ($r.Out -split "`n" | Where-Object { $_ })) {
        if ($f -match '\.(md|txt|csv|json|xml|ini|cfg|log)$') {
            $t = Get-Doc $f
            if ($null -eq $t) { continue }
            if ($t.Length -gt 16000) { $t = $t.Substring(0, 16000) + "`n[... kesildi]" }
            $parts += ('#### ' + $f + "`n" + $t.TrimEnd())
        }
        else { $parts += ('#### ' + $f + "`n(ikili dosya, eklenmedi)") }
    }
    return ($parts -join "`n`n")
}
function Get-TaskInfo([string]$fazlar, [string]$task) {
    $lines = $fazlar -split "`n"
    $rowIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match ('^\|\s*' + [regex]::Escape($task) + '\s*\|')) { $rowIdx = $i; break } }
    if ($rowIdx -lt 0) { return $null }
    $hdr = ''
    for ($j = $rowIdx - 1; $j -ge 0; $j--) { if ($lines[$j] -match '^\|\s*No\s*\|') { $hdr = $lines[$j]; break } }
    $cells = @($lines[$rowIdx].Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
    $hcells = @($hdr.Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
    $cls = ''; $dep = ''
    for ($c = 0; $c -lt $hcells.Count -and $c -lt $cells.Count; $c++) {
        if ($hcells[$c] -match (TR '^S{0131}n{0131}f')) { $cls = $cells[$c] }
        if ($hcells[$c] -match (TR '^Ba{011f}{0131}ml{0131}')) { $dep = $cells[$c] }
    }
    $deps = @()
    foreach ($m in [regex]::Matches($dep, 'T\d{3}')) {
        foreach ($l in $lines) {
            if ($l -match ('^\|\s*' + $m.Value + '\s*\|')) {
                $dc = @($l.Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
                $deps += ($m.Value + ' ' + $dc[$dc.Count - 1])
            }
        }
    }
    return [pscustomobject]@{ Row = $lines[$rowIdx]; Header = $hdr; Class = $cls; Deps = $deps; Cols = $hcells.Count }
}

# --- fark ---------------------------------------------------------------------

function Resolve-Commit([string]$c) {
    $r = Invoke-Git @('rev-parse', '--verify', '--quiet', ($c + '^{commit}')) -AllowFail
    if ($r.Code -ne 0) { Fail ('Commit bulunamadi: ' + $c + ' (git pull yaptin mi?)') }
    return $r.Out.Trim()
}
function Find-TaskCommits([string]$task, [string]$mode) {
    $r = Invoke-Git @('log', '--format=%H%x09%s', '-n', '500')
    if ($mode -eq 'fix') { $rx = '^' + [regex]::Escape($task) + (TR ' d{00fc}zeltme') } else { $rx = '^' + [regex]::Escape($task) + ':' }
    $found = @()
    foreach ($l in ($r.Out -split "`n")) {
        if (-not $l) { continue }
        $parts = @($l -split "`t", 2)
        if ($parts.Count -eq 2 -and $parts[1] -match $rx) { $found += $parts[0] }
    }
    [array]::Reverse($found)   # eskiden yeniye
    return $found
}
function Split-Units([string]$label, [string]$diff) {
    $units = @()
    $cur = New-Object System.Collections.Generic.List[string]
    foreach ($l in ($diff -split "`n")) {
        if ($l.StartsWith('diff --git ') -and $cur.Count -gt 0) {
            $units += [pscustomobject]@{ Label = $label; Text = (($cur -join "`n").TrimEnd()) }
            $cur.Clear()
        }
        $cur.Add($l)
    }
    if ($cur.Count -gt 0 -and ($cur -join '').Trim()) { $units += [pscustomobject]@{ Label = $label; Text = (($cur -join "`n").TrimEnd()) } }
    return $units
}
# Baglama sigmayan tek dosya farkini hunk sinirlarindan (gerekirse satirdan) boler.
function Split-Big($unit, [int]$budgetTokens) {
    $maxChars = [int]($budgetTokens * $CharsPerToken)
    $lines = $unit.Text -split "`n"
    $head = @(); $i = 0
    while ($i -lt $lines.Count -and -not $lines[$i].StartsWith('@@')) { $head += $lines[$i]; $i++ }
    $headText = ($head -join "`n")
    $pieces = @()
    $cur = New-Object System.Text.StringBuilder
    for (; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        $full = ($headText.Length + $cur.Length + $l.Length + 2) -gt $maxChars
        if ($cur.Length -gt 0 -and $full -and ($l.StartsWith('@@') -or $cur.Length -gt ($maxChars / 2))) { $pieces += $cur.ToString(); [void]$cur.Clear() }
        if ($cur.Length -gt 0) { [void]$cur.Append("`n") }
        [void]$cur.Append($l)
    }
    if ($cur.Length -gt 0) { $pieces += $cur.ToString() }
    $res = @()
    for ($k = 0; $k -lt $pieces.Count; $k++) {
        $res += [pscustomobject]@{ Label = ($unit.Label + ' (dosya parcasi ' + ($k + 1) + '/' + $pieces.Count + ')'); Text = ($headText + "`n" + $pieces[$k]) }
    }
    return $res
}
function Build-Chunks($units, [int]$budgetTokens) {
    $chunks = @()
    $cur = ''
    foreach ($u in $units) {
        $piece = '## ' + $u.Label + "`n" + $u.Text
        $plist = @($piece)
        if ((Est $piece) -gt $budgetTokens) { $plist = @(Split-Big $u ($budgetTokens - 200) | ForEach-Object { '## ' + $_.Label + "`n" + $_.Text }) }
        foreach ($p in $plist) {
            if ($cur -and ((Est $cur) + (Est $p)) -gt $budgetTokens) { $chunks += $cur; $cur = '' }
            if ($cur) { $cur += "`n`n" + $p } else { $cur = $p }
        }
    }
    if ($cur) { $chunks += $cur }
    return $chunks
}

# --- cevabi isleme ------------------------------------------------------------

function Norm([string]$s) {
    if (-not $s) { return '' }
    $t = $s.Replace('&#10;', ' ').Replace('&#13;', ' ').Replace('&amp;', '&').Replace('&lt;', '<').Replace('&gt;', '>').Replace('&quot;', '"')
    $t = $t.Replace('**', '').Replace('__', '').Replace('`', '')   # markdown vurgusu alintiyi bozmasin
    $t = [regex]::Replace($t, (TR '[{2018}{2019}{201a}{2032}]'), "'")
    $t = [regex]::Replace($t, (TR '[{201c}{201d}{201e}{2033}]'), '"')
    $t = [regex]::Replace($t, (TR '[{2013}{2014}{2212}]'), '-')
    $t = [regex]::Replace($t, (TR '{00d7}'), 'x')
    $t = [regex]::Replace($t, (TR '{2026}'), '...')
    $t = [regex]::Replace($t, (TR '[\s{00a0}]+'), ' ')
    return $t.Trim().ToLowerInvariant()
}
function Norm-Diff([string]$d) {
    $sb = [System.Text.StringBuilder]::new()
    foreach ($l in ($d -split "`n")) {
        if ($l.Length -gt 0 -and ($l[0] -eq '+' -or $l[0] -eq '-' -or $l[0] -eq ' ') -and -not $l.StartsWith('+++') -and -not $l.StartsWith('---')) { [void]$sb.Append($l.Substring(1)) }
        else { [void]$sb.Append($l) }
        [void]$sb.Append(' ')
    }
    return (Norm $sb.ToString())
}
function Test-Quote([string]$q, [string]$hay) {
    if (-not $q -or $q.Trim() -match '^(yok|-|none)$') { return 'verilmedi' }
    $n = Norm ($q -replace '^\s*[+\-]\s?', '')
    if ($n.Length -lt 6) { return 'kisa' }
    if ($hay.IndexOf($n, [StringComparison]::Ordinal) -ge 0) { return 'VAR' }
    if ($n.Contains('...')) {
        $ok = $true; $any = $false
        foreach ($part in ($n -split '\.\.\.')) { $pt = $part.Trim(); if ($pt.Length -ge 6) { $any = $true; if ($hay.IndexOf($pt, [StringComparison]::Ordinal) -lt 0) { $ok = $false } } }
        if ($ok -and $any) { return 'VAR (parcali)' }
    }
    if ($n.Length -gt 80 -and $hay.IndexOf($n.Substring(0, 60), [StringComparison]::Ordinal) -ge 0) { return 'VAR (ilk kismi)' }
    return 'BULUNAMADI'
}
function Get-Quote([string]$field) {
    if (-not $field) { return '' }
    $m = [regex]::Match($field, '``(.+?)``|`([^`]+)`')
    if ($m.Success) { if ($m.Groups[1].Success) { return $m.Groups[1].Value } else { return $m.Groups[2].Value } }
    return ''
}
function Get-Field([string]$block, [string]$pattern) {
    $m = [regex]::Match($block, '(?mi)^[ \t>*-]*(?:\*\*)?(?:' + $pattern + ')(?:\*\*)?[ \t]*:[ \t]*(.*)$')
    if ($m.Success) { return $m.Groups[1].Value.Trim() }
    return ''
}
function Get-Findings([string]$content, [string]$prefix) {
    $ms = [regex]::Matches($content, (TR '(?m)^[ \t]*(?:#{1,4}[ \t]*)?(?:\*\*)?B(\d{1,2})(?:\*\*)?[ \t]*[{00b7}:|\-{2013}{2014}][^\n]*$'))
    $stop = [regex]::Match($content, (TR '(?m)^[ \t]*(?:\*\*)?(?:{00d6}zet|Ozet)(?:\*\*)?[ \t]*:'))
    $res = @()
    for ($i = 0; $i -lt $ms.Count; $i++) {
        $s = $ms[$i].Index
        if ($i + 1 -lt $ms.Count) { $e = $ms[$i + 1].Index }
        else { $e = $content.Length; if ($stop.Success -and $stop.Index -gt $s) { $e = $stop.Index } }
        $block = $content.Substring($s, $e - $s)
        $head = $ms[$i].Value
        $sev = '?'
        if ($head -match (TR 'y{00fc}ksek|yuksek')) { $sev = 'yuksek' }
        elseif ($head -match '(?<![a-z])orta(?![a-z])') { $sev = 'orta' }
        elseif ($head -match (TR 'd{00fc}{015f}{00fc}k|dusuk')) { $sev = 'dusuk' }
        $res += [pscustomobject]@{
            Id = ($prefix + 'B' + $ms[$i].Groups[1].Value); Start = $s; End = $e; Block = $block; Severity = $sev
            Problem = (Get-Field $block 'Sorun'); Fix = (Get-Field $block (TR 'D(?:{00fc}|u)zeltme'))
            DiffQuote = (Get-Quote (Get-Field $block 'Fark')); DocQuote = (Get-Quote (Get-Field $block (TR 'Dok(?:{00fc}|u)man')))
            DiffCheck = ''; DocCheck = ''; Note = ''
        }
    }
    return $res
}
function Get-FixItems([string]$content, [string]$prefix) {
    $ms = [regex]::Matches($content, (TR '(?mi)^[ \t>*-]*(?:\*\*)?M(\d{1,2})(?:\*\*)?[ \t]*[:\-{2013}{00b7}][ \t]*(?:\*\*)?(d{00fc}zelmedi|duzelmedi|d{00fc}zeldi|duzeldi|emin de{011f}ilim|emin degilim)'))
    $stop = [regex]::Match($content, (TR '(?m)^[ \t]*(?:\*\*)?(?:{00d6}zet|Ozet)(?:\*\*)?[ \t]*:'))
    $res = @()
    for ($i = 0; $i -lt $ms.Count; $i++) {
        $s = $ms[$i].Index
        if ($i + 1 -lt $ms.Count) { $e = $ms[$i + 1].Index } else { $e = $content.Length; if ($stop.Success -and $stop.Index -gt $s) { $e = $stop.Index } }
        $block = $content.Substring($s, $e - $s)
        $v = $ms[$i].Groups[2].Value.ToLowerInvariant()
        if ($v -match 'medi') { $state = 'duzelmedi' } elseif ($v -match '^emin') { $state = 'emin degilim' } else { $state = 'duzeldi' }
        $res += [pscustomobject]@{ Id = ($prefix + 'M' + $ms[$i].Groups[1].Value); Start = $s; End = $e; Block = $block; State = $state; Quote = (Get-Quote (Get-Field $block (TR 'Kan(?:{0131}|i)t'))); Note = '' }
    }
    return $res
}
function Add-Notes([string]$content, $items) {
    if (@($items).Count -eq 0) { return $content }
    $sb = [System.Text.StringBuilder]::new()
    $pos = 0
    foreach ($f in $items) {
        if ($f.Start -gt $pos) { [void]$sb.Append($content.Substring($pos, $f.Start - $pos)) }
        [void]$sb.Append($content.Substring($f.Start, $f.End - $f.Start).TrimEnd())
        [void]$sb.Append("`n> betik: " + $f.Note + "`n`n")
        $pos = $f.End
    }
    if ($pos -lt $content.Length) { [void]$sb.Append($content.Substring($pos)) }
    return $sb.ToString()
}
function Read-Key([string]$path) {
    $items = @(); $thr = @{}
    foreach ($l in ((Read-Text $path) -split "`n")) {
        $t = $l.Trim()
        if (-not $t) { continue }
        if ($t -match '^#\s*esik\s*:(.*)$') {
            foreach ($kv in ($Matches[1].Trim() -split '\s+')) { $p = @($kv -split '='); if ($p.Count -eq 2) { $thr[$p[0]] = [int]$p[1] } }
            continue
        }
        if ($t.StartsWith('#')) { continue }
        $c = @($t -split '\s*\|\s*', 4)
        if ($c.Count -lt 4) { continue }
        $items += [pscustomobject]@{ Id = $c[0]; Group = $c[1]; Text = $c[2]; Rx = $c[3] }
    }
    return [pscustomobject]@{ Items = $items; Thr = $thr }
}

# --- ana akis -----------------------------------------------------------------

if ($Check) { Invoke-Check; exit 0 }
if ($Task -notmatch '^T\d{3}$') { Fail '-Task T___ biciminde olmali (ornek: -Task T311). Baglanti denemesi: -Check' }
if ($Name -and $Name -notmatch '^[A-Za-z0-9._-]+$') { Fail '-Name sadece harf, rakam, nokta, tire ve alt cizgi icerebilir' }
if (-not (Test-Path (Join-Path $Root '.git'))) { Fail ('Repo bulunamadi: ' + $Root) }

# Kaynak: -Commits > staged > gorevin commit'leri
$useStaged = $false
$commitList = @()
if ($Commits) {
    foreach ($c in ($Commits -split '[,\s]+' | Where-Object { $_ })) { $commitList += (Resolve-Commit $c) }
}
else {
    $hasStaged = ((Invoke-Git @('diff', '--cached', '--quiet') -AllowFail).Code -eq 1)
    if ($Staged -and -not $hasStaged) { Fail 'Staged degisiklik yok. Once: git add -A' }
    if ($hasStaged) { $useStaged = $true }
    else {
        $commitList = @(Find-TaskCommits $Task $Mode)
        if ($commitList.Count -eq 0) { Fail ($Task + ' icin staged degisiklik ya da commit yok (fix modunda "' + $Task + ' duzeltme" commit lerine bakilir). -Commits ile ver.') }
    }
}
if ($useStaged) { $script:DocRev = ''; $srcLabel = 'staged degisiklik' }
else {
    $script:DocRev = $commitList[$commitList.Count - 1]
    $srcLabel = 'commit ' + ((@($commitList | ForEach-Object { $_.Substring(0, 7) })) -join ', ')
}

$listText = ''
if ($Mode -eq 'fix') {
    if (-not $List) { $List = Join-Path $OutDir ('duzelt-' + $Task + '.md') } else { $List = Resolve-UserPath $List }
    if (-not (Test-Path $List)) { Fail ('DUZELT listesi yok: ' + $List + ' (listeyi bu dosyaya UTF-8 olarak kaydet ya da -List ver)') }
    $listText = (Read-Text $List).Trim()
}
$keyData = $null
if ($Key) {
    $Key = Resolve-UserPath $Key
    if (-not (Test-Path $Key)) { Fail ('Cevap anahtari yok: ' + $Key) }
    if ($Mode -ne 'review') { Warn '-Key sadece review modunda kullanilir; yok sayildi' } else { $keyData = Read-Key $Key }
}

# BAGLAM
$agents = Get-Doc 'AGENTS.md'
$fazlar = Get-Doc 'docs/04-FAZLAR.md'
if ($null -eq $agents -or $null -eq $fazlar) { Fail 'AGENTS.md ya da docs/04-FAZLAR.md okunamadi' }
$info = Get-TaskInfo $fazlar $Task
if ($null -eq $info) { Fail ($Task + ' docs/04-FAZLAR.md gorev tablolarinda yok') }

$included = @()
$ctx = [System.Text.StringBuilder]::new()
[void]$ctx.AppendLine('## ' + (TR 'G{00f6}rev sat{0131}r{0131}') + ' (docs/04-FAZLAR.md)')
[void]$ctx.AppendLine($info.Header)
[void]$ctx.AppendLine('|' + ('---|' * [Math]::Max(1, $info.Cols)))
[void]$ctx.AppendLine($info.Row)
$depText = '-'
if ($info.Deps.Count -gt 0) { $depText = ($info.Deps -join ', ') }
[void]$ctx.AppendLine()
[void]$ctx.AppendLine((TR 'S{0131}n{0131}f: ') + $info.Class + ' | ' + (TR 'Ba{011f}{0131}ml{0131} g{00f6}revlerin durumu: ') + $depText)
$included += 'docs/04-FAZLAR.md: gorev satiri'

$cfg = Read-Text (Join-Path $QwenDir 'baglam.txt')
$arrow = [string][char]0x2192
$fixed = [ordered]@{}
$mapFile = 'AGENTS.md'; $mapHead = TR 'Okuma haritas{0131}'
foreach ($l in ($cfg -split "`n")) {
    $t = $l.Trim()
    if (-not $t -or $t.StartsWith('#')) { continue }
    $isMap = $false
    if ($t.StartsWith('harita:')) { $isMap = $true; $t = $t.Substring(7).Trim() }
    $ai = $t.IndexOf($arrow)
    if ($ai -lt 0) { Warn ('baglam.txt satiri anlasilamadi: ' + $t); continue }
    $f = $t.Substring(0, $ai).Trim(); $h = $t.Substring($ai + 1).Trim()
    if ($isMap) { $mapFile = $f; $mapHead = $h; continue }
    if (-not $fixed.Contains($f)) { $fixed[$f] = @() }
    $fixed[$f] = @($fixed[$f]) + $h
}
foreach ($f in $fixed.Keys) {
    $txt = Get-Doc $f
    if ($null -eq $txt) { Warn ('Dokuman yok: ' + $f); continue }
    $sec = Get-Sections $txt @($fixed[$f]) $f
    if (-not $sec) { continue }
    [void]$ctx.AppendLine()
    [void]$ctx.AppendLine('## ' + $f + ' ' + $arrow + ' ' + (@($fixed[$f]) -join ', '))
    [void]$ctx.AppendLine($sec)
    $included += ($f + ': ' + (@($fixed[$f]) -join ', '))
}
$mapText = Get-Doc $mapFile
if ($null -eq $mapText) { Fail ('Okuma haritasi okunamadi: ' + $mapFile) }
$plan = Get-MapPlan $mapText $mapHead $Task
foreach ($p in $plan.Keys) {
    $e = $plan[$p]
    $body = ''; $title = $p
    if ($e.Mode -eq 'dir') { $body = Get-DirDocs $p }
    else {
        $txt = Get-Doc $p
        if ($null -eq $txt) { Warn ('Dokuman yok: ' + $p); continue }
        if ($e.Mode -eq 'whole') { $body = $txt.TrimEnd() }
        elseif ($e.Mode -eq 'sections') { $body = Get-Sections $txt @($e.Names) $p; $title = $p + ' ' + $arrow + ' ' + (@($e.Names) -join ', ') }
        else { $body = Get-TaskBlocks $txt $Task; $title = $p + ' (' + $Task + (TR ' ge{00e7}en b{00f6}l{00fc}mler)') }
    }
    if (-not $body) { continue }
    [void]$ctx.AppendLine()
    [void]$ctx.AppendLine('## ' + $title)
    [void]$ctx.AppendLine($body)
    $included += ($p + ': ' + $e.Mode + $(if ($e.Mode -eq 'sections') { ' (' + (@($e.Names) -join ', ') + ')' } else { '' }))
}
if (-not $useStaged) {
    [void]$ctx.AppendLine()
    [void]$ctx.AppendLine('## ' + (TR 'Yazan{0131}n notu (commit mesajlar{0131})'))
    foreach ($c in $commitList) {
        $msg = (Invoke-Git @('log', '-1', '--format=%h %s%n%n%b', $c)).Out.TrimEnd()
        [void]$ctx.AppendLine()
        [void]$ctx.AppendLine('### ' + $msg)
    }
}
$ctxText = $ctx.ToString().TrimEnd()

# FARK
$units = @()
if ($useStaged) {
    $d = (Invoke-Git @('diff', '--cached', '--no-color', '-M')).Out
    $units = @(Split-Units (TR 'staged de{011f}i{015f}iklik (hen{00fc}z commit edilmedi)') $d)
}
else {
    foreach ($c in $commitList) {
        $subj = (Invoke-Git @('log', '-1', '--format=%h %s', $c)).Out.Trim()
        $d = (Invoke-Git @('show', '--no-color', '-M', '--format=', $c)).Out
        $units += @(Split-Units ('commit ' + $subj) $d)
    }
}
if ($units.Count -eq 0) { Fail 'Fark bos' }

$tplName = 'on-inceleme.md'
if ($Mode -eq 'fix') { $tplName = 'duzeltme-kontrolu.md' }
$tpl = (Read-Text (Join-Path $QwenDir $tplName)).Replace('{{GOREV}}', $Task).Replace('{{LISTE}}', $listText).Replace('{{BAGLAM}}', $ctxText)
$noteMulti = TR 'Bu, fark{0131}n {0}/{1} par{00e7}as{0131}. Di{011f}er par{00e7}alardaki dosyalar{0131} g{00f6}rm{00fc}yorsun; onlar hakk{0131}nda tahmin y{00fc}r{00fc}tme.'
$baseTok = Est ($tpl.Replace('{{PARCA}}', ($noteMulti -f 99, 99)).Replace('{{FARK}}', ''))
$diffBudget = $Context - $MinOutput - $baseTok - 600
if ($diffBudget -lt 1500) { Fail ('Baglam yetmiyor: paketin dokuman kismi ~' + $baseTok + ' token, baglam ' + $Context + '. OLLAMA_CONTEXT_LENGTH ya da -Context degerini buyut (05-HANDOFF -> 0.5).') }
$chunks = @(Build-Chunks $units $diffBudget)
$fence = '~~~~~~'
$packets = @()
for ($i = 0; $i -lt $chunks.Count; $i++) {
    if ($chunks.Count -eq 1) { $note = TR 'Fark{0131}n tamam{0131} a{015f}a{011f}{0131}da.' } else { $note = $noteMulti -f ($i + 1), $chunks.Count }
    $packets += $tpl.Replace('{{PARCA}}', $note).Replace('{{FARK}}', ($fence + "diff`n" + $chunks[$i] + "`n" + $fence))
}

$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
if ($Name) { $base = $Name } else { $base = $Task + '-' + $Mode + '-' + $stamp }
$pathOut = Join-Path $OutDir ($base + '.md')
$pathThink = Join-Path $OutDir ($base + '-dusunme.md')
$pathPacket = Join-Path $OutDir ($base + '-paket.md')
$sep = "`n`n<!-- ===================== parca sonu ===================== -->`n`n"
Write-Text $pathPacket ($packets -join $sep)

Say ('Qwen ' + $Mode + ' | ' + $Task + ' (sinif ' + $info.Class + ') | kaynak: ' + $srcLabel + ' | model ' + $Model + ' | baglam ' + $Context)
Say 'Pakete giren dokumanlar:'
foreach ($x in $included) { Say ('  - ' + $x) }
Say ('Dokuman kismi ~' + $baseTok + ' token; fark ' + $units.Count + ' dosya parcasi, ' + $chunks.Count + ' paket (tahmin: karakter/' + $CharsPerToken + ')')
for ($i = 0; $i -lt $packets.Count; $i++) {
    $it = Est $packets[$i]
    Say ('  paket ' + ($i + 1) + '/' + $packets.Count + ': ~' + $it + ' token girdi, en fazla ' + ($Context - $it - 256) + ' token cikti')
}
Say ('Paket dosyasi: ' + $pathPacket)
if ($DryRun) { Say 'DryRun: model cagrilmadi.'; exit 0 }

# Modeli cagir
$results = @()
for ($i = 0; $i -lt $packets.Count; $i++) {
    $inTok = Est $packets[$i]
    $np = $Context - $inTok - 256
    Say ('Paket ' + ($i + 1) + '/' + $packets.Count + ': Qwen calisiyor (ilk cevap 30-90 sn surebilir; toplam birkac dakika)...')
    $r = Invoke-Chat $packets[$i] $np
    $results += [pscustomobject]@{ Index = $i; Chunk = $chunks[$i]; R = $r; InTok = $inTok; NumPredict = $np }
}

$ctxNorm = Norm ($ctxText + "`n" + $listText)
$exitCode = 0
$total = 0.0
$hdr = New-Object System.Collections.Generic.List[string]
$bodyOut = New-Object System.Collections.Generic.List[string]
$thinkOut = New-Object System.Collections.Generic.List[string]
$allItems = @()
$titleMode = 'on inceleme'
if ($Mode -eq 'fix') { $titleMode = 'duzeltme kontrolu' }
$hdr.Add('# Qwen ' + $titleMode + ' - ' + $Task)
$hdr.Add('')
$hdr.Add('- Kaynak: ' + $srcLabel + ' | Model: ' + $Model + ' | Baglam: ' + $Context + ' | Tarih: ' + $stamp)
foreach ($x in $results) {
    $r = $x.R
    $total += $r.Seconds
    $fin = $r.Final
    $pe = Get-Prop $fin 'prompt_eval_count'; $ec = Get-Prop $fin 'eval_count'; $ed = Get-Prop $fin 'eval_duration'; $dr = Get-Prop $fin 'done_reason'
    $rate = ''
    if ($ec -and $ed) { $rate = (' | ' + [Math]::Round([double]$ec / ([double]$ed / 1e9), 1) + ' token/sn') }
    $pn = ($x.Index + 1).ToString() + '/' + $results.Count
    $hdr.Add('- Paket ' + $pn + ': girdi ~' + $x.InTok + ' token (tahmin), Qwen okudu ' + $pe + ', yazdi ' + $ec + ' (dusunme ~' + $r.NThink + ')' + $rate + ' | bitis: ' + $dr + ' | ' + (Format-Sec $r.Seconds))
    if ($dr -eq 'length') { Warn ('Paket ' + $pn + ': cikti siniri doldu, cevap yarim olabilir (baglami buyut ya da -Think medium dene)'); $exitCode = 3 }
    if (-not $r.Content.Trim()) { Warn ('Paket ' + $pn + ': cevap bos'); $exitCode = 3 }
    $prefix = ''
    if ($results.Count -gt 1) { $prefix = 'P' + ($x.Index + 1) + '-' }
    $diffNorm = Norm-Diff $x.Chunk
    if ($Mode -eq 'review') {
        $items = @(Get-Findings $r.Content $prefix)
        foreach ($f in $items) {
            $f.DiffCheck = Test-Quote $f.DiffQuote $diffNorm
            $f.DocCheck = Test-Quote $f.DocQuote $ctxNorm
            $warnTxt = ''
            if ($f.DiffCheck -eq 'BULUNAMADI' -or $f.DocCheck -eq 'BULUNAMADI') { $warnTxt = ' -> UYDURMA OLABILIR, dogrula' }
            $f.Note = 'fark alintisi ' + $f.DiffCheck + ' | dokuman alintisi ' + $f.DocCheck + $warnTxt
        }
    }
    else {
        $items = @(Get-FixItems $r.Content $prefix)
        foreach ($f in $items) { $q = Test-Quote $f.Quote $diffNorm; $f.Note = 'kanit alintisi ' + $q; if ($q -eq 'BULUNAMADI') { $f.Note += ' -> dogrula' } }
    }
    $allItems += $items
    if ($results.Count -gt 1) { $bodyOut.Add('## Paket ' + $pn); $bodyOut.Add('') }
    $bodyOut.Add((Add-Notes $r.Content $items).TrimEnd())
    $bodyOut.Add('')
    $thinkOut.Add('## Paket ' + $pn)
    $thinkOut.Add($r.Thinking)
}
$hdr.Add('- Toplam sure: ' + (Format-Sec $total))

if ($Mode -eq 'review') {
    $cnt = @{ yuksek = 0; orta = 0; dusuk = 0; '?' = 0 }
    foreach ($f in $allItems) { $cnt[$f.Severity]++ }
    $bad = @($allItems | Where-Object { $_.DiffCheck -eq 'BULUNAMADI' -or $_.DocCheck -eq 'BULUNAMADI' } | ForEach-Object { $_.Id })
    $badText = '-'
    if ($bad.Count -gt 0) { $badText = ($bad -join ', ') + ' (uydurma olabilir)' }
    $hdr.Add('- Bulgular: ' + $allItems.Count + ' (yuksek ' + $cnt['yuksek'] + ', orta ' + $cnt['orta'] + ', dusuk ' + $cnt['dusuk'] + ', belirsiz ' + $cnt['?'] + ') | alintisi bulunamayan: ' + $badText)
    if ($null -ne $keyData) {
        $hdr.Add('')
        $hdr.Add('## Cevap anahtari: ' + [IO.Path]::GetFileName($Key))
        $hdr.Add('')
        $hdr.Add('| Kimlik | Grup | Bulundu | Bulgu | Aciklama |')
        $hdr.Add('|---|---|---|---|---|')
        $matched = @{}
        $grp = [ordered]@{}
        foreach ($k in $keyData.Items) {
            if (-not $grp.Contains($k.Group)) { $grp[$k.Group] = @(0, 0) }
            $grp[$k.Group][1]++
            $hits = @()
            foreach ($f in $allItems) {
                $txt = ($f.Problem + "`n" + $f.Fix).Trim()
                if (-not $txt) { $txt = [regex]::Replace($f.Block, (TR '(?mi)^.*(Fark|Dok({00fc}|u)man|Kan({0131}|i)t)\s*:.*$'), '') }
                $ok = $false
                try { $ok = [regex]::IsMatch($txt, $k.Rx, 'IgnoreCase') } catch { Warn ('Anahtar deseni gecersiz: ' + $k.Id) }
                if ($ok) { $hits += $f.Id; $matched[$f.Id] = $true }
            }
            $yes = 'hayir'
            if ($hits.Count -gt 0) { $yes = 'EVET'; $grp[$k.Group][0]++ }
            $hdr.Add('| ' + $k.Id + ' | ' + $k.Group + ' | ' + $yes + ' | ' + ($hits -join ', ') + ' | ' + $k.Text + ' |')
        }
        $hdr.Add('')
        foreach ($g in @($grp.Keys)) {
            $line = '- ' + $g + ': ' + $grp[$g][0] + '/' + $grp[$g][1]
            if ($keyData.Thr.ContainsKey($g)) { $okTxt = 'KALDI'; if ($grp[$g][0] -ge $keyData.Thr[$g]) { $okTxt = 'TAMAM' }; $line += ' (esik ' + $keyData.Thr[$g] + ') -> ' + $okTxt }
            $hdr.Add($line)
        }
        if ($keyData.Thr.ContainsKey('toplam')) { $okTxt = 'KALDI'; if ($allItems.Count -le $keyData.Thr['toplam']) { $okTxt = 'TAMAM' }; $hdr.Add('- toplam bulgu: ' + $allItems.Count + ' (en fazla ' + $keyData.Thr['toplam'] + ') -> ' + $okTxt) }
        $un = @($allItems | Where-Object { -not $matched.ContainsKey($_.Id) } | ForEach-Object { $_.Id })
        $unText = '-'
        if ($un.Count -gt 0) { $unText = ($un -join ', ') + ' -> gercek mi yanlis mi, Sonnet ile bak (05-HANDOFF -> 9.4)' }
        $hdr.Add('- anahtarla eslesmeyen: ' + $unText)
        $hdr.Add('- uydurma (alintisi bulunamayan + dokumanla celisen): en fazla 2 olmali; alintisi bulunamayan: ' + $badText)
    }
}
else {
    $st = @{ 'duzeldi' = 0; 'duzelmedi' = 0; 'emin degilim' = 0 }
    foreach ($f in $allItems) { $st[$f.State]++ }
    $hdr.Add('- Maddeler: ' + $allItems.Count + ' | duzeldi ' + $st['duzeldi'] + ', duzelmedi ' + $st['duzelmedi'] + ', emin degilim ' + $st['emin degilim'])
    $hdr.Add('- ' + ((@($allItems | ForEach-Object { $_.Id + ' ' + $_.State })) -join ' | '))
}

$doc = ($hdr -join "`n") + "`n`n---`n`n" + ($bodyOut -join "`n")
Write-Text $pathOut $doc
Write-Text $pathThink ($thinkOut -join "`n`n")
if (-not $Name) { Write-Text (Join-Path $OutDir ($Task + '-' + $Mode + '-son.md')) $doc }
if (-not $Quiet) { Say ''; Say $doc; Say '' }
Say ('Sonuc: ' + $pathOut)
Say ('Dusunme: ' + $pathThink)
exit $exitCode
