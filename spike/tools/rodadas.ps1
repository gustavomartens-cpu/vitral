# Conduz as rodadas da fase 0: reinicia o Explorer, carrega a DLL pela rota pedida,
# pergunta o que você viu e registra tudo em spike\resultados\rotas.csv e visual.csv.
param(
    [ValidateSet('externa', 'interna')] [string] $Rota = 'externa',
    [int] $N = 10
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'env.ps1')
$spike    = Resolve-Path (Join-Path $PSScriptRoot '..')
$injector = Join-Path $spike 'injector\bin\Release\net10.0-windows\Injector.exe'
$visual   = Join-Path $spike 'resultados\visual.csv'

if (Get-Process TranslucentTB -ErrorAction SilentlyContinue) { throw 'Feche o TranslucentTB antes (bandeja > Sair).' }

for ($i = 1; $i -le $N; $i++) {
    Write-Host "`n===== Rota $Rota · rodada $i de $N =====" -ForegroundColor Cyan
    Write-Host 'Reiniciando o Explorer...'
    Stop-Process -Name explorer -Force
    do { Start-Sleep -Milliseconds 500 } until (Get-Process explorer -ErrorAction SilentlyContinue)
    Start-Sleep -Seconds 6   # deixa a barra terminar de montar

    # Com sucesso, o injector mostra o resultado e espera Enter; ao sair, o componente restaura a barra.
    & $injector --rota $Rota --alvo explorer --modo transparente
    $codigo = $LASTEXITCODE
    $viu    = Read-Host 'Antes de você apertar Enter, a(s) barra(s) de tarefas ficaram transparentes? (s/n)'
    $voltou = '-'
    if ($viu -eq 's') {
        Start-Sleep -Seconds 2
        $voltou = Read-Host 'E agora, depois do Enter, as barras voltaram ao normal? (s/n)'
    }
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss');$Rota;$i;codigo=$codigo;transparente=$viu;restaurou=$voltou" | Add-Content $visual
}
Write-Host "`nFim da rota $Rota. Resultados em $spike\resultados\" -ForegroundColor Green
