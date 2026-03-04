# Analisis Inicial - SQLmanagerAvz4

Fecha: 2026-02-21  
Alcance: revision estatica inicial del codigo (sin compilacion local por falta de `dotnet` en el entorno).

## 1) Init realizado

- Repositorio inicializado con `git init` en `/workspace/SQLmanagerAvz4`.
- Estado actual: repo sin commits, arbol detectado correctamente.

## 2) Snapshot tecnico rapido

- Stack principal: `net9.0` + Avalonia `11.3.3` + Oracle ManagedDataAccess (`Oracle.ManagedDataAccess.Core` 23.9.1).
- Solucion/proyecto: `SQLmanager.sln`, `SQLmanager.csproj`.
- Tamano aproximado:
  - 42 archivos `.cs`
  - 5 archivos `.axaml`
  - archivo mas grande: `ConsultaWindow.xaml.cs` (~182 KB)

## 3) Hallazgos priorizados

## Criticos

1. Exposicion de credenciales en codigo fuente
- Evidencia: `ConnectionTest.cs:15`, `ConnectionTest.cs:16`, `ConnectionTest.cs:17`.
- Riesgo: fuga directa de usuario/password/host en repo, logs, backups, historial.
- Impacto: alto en seguridad y cumplimiento.
- Accion: eliminar credenciales hardcodeadas; usar variables de entorno o secretos por entorno.

2. Log de connection string completa (incluye password)
- Evidencia: `Services/DatabaseService.cs:80`.
- Riesgo: la password termina en logs/console, incluso si hay version "sanitizada" en otra linea.
- Impacto: compromiso de credenciales en runtime.
- Accion: eliminar ese log o forzar sanitizacion estricta antes de cualquier salida.

## Altos

3. Credenciales/host por defecto embebidos en DI
- Evidencia: `App.axaml.cs:118`, `App.axaml.cs:119`, `App.axaml.cs:150`, `App.axaml.cs:151`.
- Riesgo: conexion accidental a host interno y dependencia no portable entre entornos.
- Impacto: errores operativos y posible fuga de topologia interna.
- Accion: quitar defaults productivos; usar solo configuracion externa valida.

4. Construccion de SQL dinamico con identificadores interpolados
- Evidencia: `ConsultaWindow.xaml.cs:1822`, `ConsultaWindow.xaml.cs:1833`, `ConsultaWindow.xaml.cs:2599`, `ConsultaWindow.xaml.cs:2610`.
- Riesgo: aunque los valores van parametrizados, `tabla`/`campo`/`columnName` van por interpolacion.
- Impacto: superficie de inyeccion/logica si entra un identificador no esperado.
- Accion: reemplazar por whitelist estricta de columnas/tablas permitidas y mapear IDs internos -> nombre real.

5. Flujo de password en texto plano en varias capas
- Evidencia: `Security/Manager.cs:26`, `Security/Manager.cs:68`, `MainWindow.axaml.cs:149`, `MainWindowViewModel.cs:58`.
- Riesgo: password pasa por `string` y UI (`TextBox`) con mayor permanencia en memoria.
- Impacto: aumento de exposicion en memoria y logs/debug.
- Accion: minimizar ciclo de vida en `string`, usar controles/flujo seguro y centralizar limpieza.

## Medios

6. Uso de `ExecuteQuery` para `INSERT`
- Evidencia: `InsertarPedidoWindow.cs:191`, `InsertarPedidoWindow.cs:193`.
- Riesgo: semantica incorrecta (deberia ser `ExecuteNonQuery`), posible comportamiento inesperado y deuda tecnica.
- Impacto: mantenibilidad y errores funcionales futuros.
- Accion: cambiar a `ExecuteNonQuery` y validar resultado de filas afectadas.

7. Clase monolitica de UI con alta complejidad
- Evidencia: `ConsultaWindow.xaml.cs` (~182 KB).
- Riesgo: alto acoplamiento, dificil testing, regresiones frecuentes.
- Impacto: velocidad de desarrollo baja y bugs en cambios pequenos.
- Accion: extraer por casos de uso (queries, paginacion, edicion inline, export, borrado) en servicios/handlers.

8. Recurso Avalonia duplicado
- Evidencia: `SQLmanager.csproj:45` y `SQLmanager.csproj:46` (`Assets/*` duplicado).
- Riesgo: empaquetado redundante/confuso.
- Impacto: build output menos limpio.
- Accion: eliminar duplicado.

9. `async void` en handlers relevantes
- Evidencia: `MainWindow.axaml.cs:172`, `ConsultaWindow.xaml.cs:1095`, `ConsultaWindow.xaml.cs:2646`, `ConsultaWindow.xaml.cs:3394`.
- Riesgo: excepciones no observadas y trazabilidad limitada.
- Impacto: fallos intermitentes de UI.
- Accion: mantener `async void` solo en eventos UI inevitables, con manejo robusto y encapsulando logica en `Task` testeable.

## 4) Fortalezas detectadas

- Hay uso extensivo de parametros SQL en valores (reduce riesgo de inyeccion por valores).
- Existe capa de auditoria (`IAuditLogger`) y manejo de credenciales por plataforma (Windows/macOS).
- Validaciones defensivas en `DAO` (limites de parametros y controles basicos).

## 5) Plan recomendado (orden de ejecucion)

1. Seguridad inmediata (prioridad maxima)
- Quitar credenciales hardcodeadas y defaults de host/usuario.
- Eliminar logs con secretos (`DatabaseService`).
- Revisar y reducir conversiones `SecureString -> string`.

2. Robustez de datos
- Sustituir SQL dinamico de identificadores por whitelist/mapeos cerrados.
- Corregir `ExecuteQuery` -> `ExecuteNonQuery` en inserts/updates/deletes.

3. Refactor estructural
- Dividir `ConsultaWindow.xaml.cs` en modulos verticales.
- Mover SQL y reglas de negocio a servicios/repositorios dedicados.

4. Calidad de entrega
- Agregar pipeline de build/test.
- Estandarizar logging seguro (nunca secretos) y niveles de log.

## 6) Bloqueos de esta revision

- No se pudo ejecutar compilacion local porque `dotnet` no esta instalado en este entorno (`command not found`).

