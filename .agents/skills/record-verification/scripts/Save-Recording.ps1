param(
    [Parameter(Mandatory)][string]$Source,
    [Parameter(Mandatory)][string]$Title
)

$ErrorActionPreference = 'Stop'
$video = Get-Item -LiteralPath $Source
if ($video.PSIsContainer -or $video.Extension -ine '.mp4' -or $video.Length -eq 0) {
    throw '재생을 확인한 MP4 파일이 필요합니다.'
}

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::DesktopDirectory)
if ([string]::IsNullOrWhiteSpace($desktop)) {
    throw 'Windows 바탕화면 경로를 찾을 수 없습니다.'
}
$folder = [IO.Directory]::CreateDirectory([IO.Path]::Combine($desktop, '녹화'))
$label = ($Title -replace '[<>:"/\\|?*\x00-\x1f]', '_').Trim().TrimEnd('.')
if (!$label) { $label = '검증' }
$stem = (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $label
$destination = [IO.Path]::Combine($folder.FullName, "$stem.mp4")
if ([IO.File]::Exists($destination)) {
    $destination = [IO.Path]::Combine($folder.FullName, "$stem-$([Guid]::NewGuid().ToString('N')).mp4")
}
[IO.File]::Copy($video.FullName, $destination, $false)
$destination
