# Records every line of Narration/narration.txt as Narration/<id>.wav using a Windows voice.
# Run it again after editing narration.txt, then run Voice Game > Rebuild Audio Edition Scenes in Unity.
param([string]$Voice = 'Microsoft Zira Desktop', [int]$Rate = 0)

$folder = Join-Path $PSScriptRoot '..\Narration'
Add-Type -AssemblyName System.Speech
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(22050, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)
$count = 0
foreach ($line in [IO.File]::ReadAllLines((Join-Path $folder 'narration.txt'))) {
    if ($line.Trim().Length -eq 0) { continue }
    $parts = $line.Split('|', 2)
    $speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
    $speaker.SelectVoice($Voice)
    $speaker.Rate = $Rate
    $speaker.SetOutputToWaveFile((Join-Path $folder ($parts[0] + '.wav')), $format)
    $speaker.Speak($parts[1])
    $speaker.Dispose()
    $count++
}
"Recorded $count lines with $Voice"
