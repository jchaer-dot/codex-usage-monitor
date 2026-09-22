# Guía de instalación y uso

Codex Usage Monitor 0.5.0 beta | Windows x64 | Gratuito y de código abierto

## 1. Antes de empezar

Necesitas Windows 10/11 de 64 bits y Codex instalado con una sesión válida. El widget usa el ejecutable nativo codex.exe y su comando app-server. No necesita tu contraseña ni una API key propia. Algunas métricas dependen de la versión y del tipo de cuenta. El paquete portable incluye .NET.

## 2. Descargar y abrir

Visita https://github.com/jchaer-dot/codex-usage-monitor/releases y descarga CodexUsageMonitor-0.5.0-win-x64.zip. Haz clic derecho en el ZIP, selecciona Extraer todo y guarda la carpeta en una ubicación permanente. Abre CodexUsageMonitor.exe. No lo ejecutes directamente dentro del ZIP.

La primera lectura del historial puede tardar. Los datos de cuenta se consultan cada minuto y el ranking local se vuelve a revisar aproximadamente cada cinco minutos.

El ejecutable no está firmado digitalmente. Si Windows muestra una advertencia, verifica el origen y el SHA-256 publicado antes de decidir ejecutarlo. No desactives el antivirus. Puedes compilar el código tú mismo si prefieres no ejecutar el binario distribuido.

## 3. Si aparece CODEX NOT FOUND

El widget busca codex.exe en la instalación local de OpenAI/Codex y en PATH. Si tu instalación está en otra ubicación, configura la variable de entorno de usuario CODEX_WIDGET_CLI con la ruta completa al codex.exe nativo y vuelve a abrir el widget. Un archivo codex.cmd de npm no es un ejecutable nativo y no funciona como ruta directa.

Para comprobar la instalación en PowerShell: codex --version. Si la sesión no está iniciada, autentícate mediante el flujo oficial de Codex. No compartas archivos auth.json, cookies ni claves.

## 4. Tamaño y controles

Clic derecho: Tamaño compacto, Tamaño normal, bloquear/desbloquear posición, Uso de cuenta y Cerrar. Arrastra el encabezado para moverlo y los bordes derecho/inferior para redimensionar cuando esté desbloqueado. El modo normal amplía texto y muestra más modelos y proyectos, además del gráfico.

Los botones 7d, 30d y Todo cambian el intervalo. Haz clic en TOP LOCAL para abrir el ranking ampliado. La X cierra el widget y detiene las alertas; no lo minimiza a la bandeja.

## 5. Interpretar el consumo

El porcentaje principal es saldo semanal RESTANTE. Los avisos se producen al alcanzar o bajar de 70%, 50% y 30% restante y se recuerdan por ciclo. Windows puede ocultar notificaciones si está activo No molestar. Si abres el widget con saldo ya bajo, puede avisar en la primera lectura.

Los tokens por modelo/proyecto son una agregación local parcial, no créditos ni dinero. Pueden incluir otras cuentas usadas en este equipo. La cuota semanal y el gráfico oficial pueden tener distinta cobertura respecto al ranking local. No sumes estas métricas como si fueran equivalentes. La guía de ritmo diario es orientativa.

## 6. Inicio automático opcional

Para abrirlo al iniciar sesión en Windows, crea un acceso directo a CodexUsageMonitor.exe. Pulsa Win+R, escribe shell:startup y coloca allí el acceso directo. No muevas después la carpeta del programa sin actualizar ese acceso directo. Esta función es opcional y no se activa automáticamente al abrir el ZIP.

## 7. Problemas frecuentes

CONNECTION ERROR: verifica que Codex funciona y tiene sesión iniciada; reinicia el widget. Las consultas tienen un tiempo límite y vuelven a intentarse en el siguiente refresco. SIN DATOS o --: la cuenta o versión de Codex no devolvió esa métrica; no significa que se haya agotado la cuota. Ranking vacío: todavía no hay registros compatibles en el historial local. Abre una sola instancia para evitar avisos duplicados.

Si persiste un fallo, abre un issue en GitHub con versión de Windows, versión de Codex y pasos. Antes de adjuntar capturas, oculta nombres de proyectos y datos de tu cuenta.

## 8. Actualizar o desinstalar

Cierra el widget antes de sustituirlo por una versión nueva. Para desinstalar, ciérralo, elimina su carpeta y, si lo creaste, su acceso directo de Inicio. Opcionalmente elimina %APPDATA%\CodexUsageMonitor para borrar preferencias. No borres tu carpeta .codex: contiene datos de Codex ajenos al widget.

Proyecto independiente, no afiliado a OpenAI. Software beta bajo licencia MIT, sin garantía. Código y novedades: https://github.com/jchaer-dot/codex-usage-monitor
