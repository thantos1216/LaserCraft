# Original LaserCraft icon. Requires Windows System.Drawing; no runtime app dependency.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$output=Join-Path (Split-Path -Parent $PSScriptRoot) 'src/LaserCraft.Desktop/Assets/LaserCraft.ico'
$sizes=@(16,24,32,48,64,128,256)
$images=@()
foreach($size in $sizes){
 $bitmap=[Drawing.Bitmap]::new($size,$size);$graphics=[Drawing.Graphics]::FromImage($bitmap)
 try{
  $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias;$graphics.ScaleTransform($size/256.0,$size/256.0)
  $background=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#101C2C'));$teal=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#19D3CF'));$light=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#E4FDFF'),6);$spark=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#FFAD56'),5)
  try{
   $graphics.FillRectangle($background,0,0,256,256)
   $points=[Drawing.Point[]]@([Drawing.Point]::new(48,48),[Drawing.Point]::new(82,48),[Drawing.Point]::new(82,176),[Drawing.Point]::new(204,176),[Drawing.Point]::new(204,210),[Drawing.Point]::new(48,210))
   $graphics.FillPolygon($teal,$points);$graphics.DrawLine($light,195,40,111,150)
   $graphics.DrawLine($spark,103,144,103,169);$graphics.DrawLine($spark,91,156,116,156);$graphics.DrawLine($spark,94,147,113,166)
  }finally{$background.Dispose();$teal.Dispose();$light.Dispose();$spark.Dispose()}
  $stream=[IO.MemoryStream]::new();try{$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$images+=,@($stream.ToArray())}finally{$stream.Dispose()}
 }finally{$graphics.Dispose();$bitmap.Dispose()}
}
$file=[IO.File]::Create($output);$writer=[IO.BinaryWriter]::new($file)
try{
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
 for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset);$offset+=$images[$i].Length}
 foreach($bytes in $images){$writer.Write([byte[]]$bytes)}
}finally{$writer.Dispose();$file.Dispose()}
