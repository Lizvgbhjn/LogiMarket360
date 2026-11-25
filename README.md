# LogiMarket 360 - Plataforma Integral de Logística y Ventas
**Guía de Práctica Experimental 04 (APE 04)**  
*Desarrollo Integral con Clean Architecture, Clean Code, Principios SOLID y 23 Patrones GoF en .NET 8 (C# / WinForms / SQLite)*

---

## 1. Resumen del Proyecto

**LogiMarket 360** es una solución empresarial de escritorio desarrollada en .NET 8 (C# 12 y Windows Forms) para la gestión integral de catálogo multinivel (productos simples y kits compuestos anidados), pedidos, cobros seguros con prevención de duplicados (idempotencia), control de existencias a nivel terminal, despacho físico por roles, devoluciones auditadas y generación desacoplada de comprobantes.

El sistema fue concebido bajo **Clean Architecture**, aplicando rigurosamente los principios **SOLID**, prácticas de **Clean Code**, y la implementación tangible y justificada de los **23 patrones de diseño clásicos de Gang of Four (GoF)**. La persistencia es 100% real mediante **Entity Framework Core 8** sobre **SQLite**, soportando transacciones ACID, índices únicos, bloqueos optimistas para concurrencia y el patrón transaccional **Outbox** para eventos asíncronos.

---

## 2. Estructura de la Solución (Clean Architecture)

```
LogiMarket360/
├── src/
│   ├── LogiMarket.Domain/           # Capa Central: Entidades puras, Value Objects, Excepciones y contratos núcleo
│   ├── LogiMarket.Application/      # Capa de Aplicación: Casos de uso, Fachadas, Validadores, DTOs y Puertos
│   ├── LogiMarket.Infrastructure/   # Capa de Infraestructura: EF Core SQLite, Adaptadores, Proveedores, Outbox
│   └── LogiMarket.WinForms/         # Capa de Presentación: MainForm, 12 Vistas modulares (UserControls), DI Host
├── tests/
│   └── LogiMarket.Tests/            # Suite de Pruebas Automatizadas xUnit (casos P01 a P38)
├── LogiMarket360.sln                # Archivo de solución maestro de Visual Studio / .NET CLI
└── README.md                        # Documentación técnica y guía de puesta en marcha
```

### Reglas de Dependencia
- `LogiMarket.Domain` no depende de ninguna otra capa ni de frameworks externos.
- `LogiMarket.Application` depende únicamente de `LogiMarket.Domain`.
- `LogiMarket.Infrastructure` implementa los puertos de `LogiMarket.Application` y depende de `LogiMarket.Domain`.
- `LogiMarket.WinForms` actúa como Composition Root (junto con Microsoft.Extensions.DependencyInjection) y orquesta la UI consumiendo los casos de uso a través del `IFormularioPrincipalMediator`.
- `LogiMarket.Tests` prueba exhaustivamente todas las capas sin acoplarse a controles visuales de WinForms.

---

## 3. Matriz de los 23 Patrones de Diseño GoF Implementados

| Categoría | Patrón GoF | Ubicación en Código | Propósito y Aplicación en LogiMarket 360 |
| :--- | :--- | :--- | :--- |
| **Creacional** | **Factory Method** | `src/LogiMarket.Infrastructure/FactoryMethod/NotificadoresFactoryMethod.cs` | Factoría extensible para instanciar notificadores (`EmailNotificadorCreator`, `SmsNotificadorCreator`) según la preferencia del cliente. |
| **Creacional** | **Abstract Factory** | `src/LogiMarket.Infrastructure/AbstractFactory/FamiliasEntrega.cs` | Creación coordinada de familias completas de entrega (`EstandarEntregaFactory`, `ExpresEntregaFactory`: operador, tarifa, etiqueta y plazo). |
| **Creacional** | **Builder** | `src/LogiMarket.Application/Builder/PedidoBuilder.cs` | Construcción paso a paso de pedidos complejos (cliente, líneas, kits, tipo de envío, validación de estado inicial). |
| **Creacional** | **Prototype** | `src/LogiMarket.Application/Prototype/PedidoBorradorPrototype.cs` | Clonación profunda (`Clonar()`) de borradores de pedidos recurrentes para agilizar compras sin reescribir líneas. |
| **Creacional** | **Singleton** | `src/LogiMarket.Application/Singleton/MetricasSesion.cs` | Registro global thread-safe en memoria de métricas de sesión de venta (pedidos confirmados, montos acumulados, transacciones). |
| **Estructural** | **Adapter** | `src/LogiMarket.Infrastructure/Adapter/PasarelasSimuladas.cs` | Homologa pasarela bancaria heredada en centavos (`PasarelaLegadaSimulada`) al contrato estándar `IPasarelaPago`. |
| **Estructural** | **Bridge** | `src/LogiMarket.Infrastructure/Bridge/ComprobantesBridge.cs` | Desacopla la jerarquía de comprobantes (`ComprobanteVenta`, `ComprobanteDevolucion`) del medio de emisión (`VistaPreviaSalida`, `ArchivoTxtSalida`). |
| **Estructural** | **Composite** | `src/LogiMarket.Domain/Composite/KitCompuesto.cs` | Tratamiento uniforme de ítems individuales (`ProductoSimple`) y agrupaciones (`KitCompuesto` anidados: KIT_OFICINA, KIT_TELETRABAJO). |
| **Estructural** | **Decorator** | `src/LogiMarket.Infrastructure/Decorator/AuditoriaPagoDecorator.cs` | Envoltorio transparente sobre `IPasarelaPago` para interceptar, auditar y medir tiempos de cobro sin modificar pasarelas. |
| **Estructural** | **Facade** | `src/LogiMarket.Application/Facade/ConfirmacionPedidoFacade.cs` | Fachada unificada que coordina validación, cobro, transacción ACID, descuento de stock terminal, outbox y notificación. |
| **Estructural** | **Flyweight** | `src/LogiMarket.Infrastructure/Flyweight/CatalogoVisualFlyweight.cs` | Reutilización de metadatos pesados (iconos, categorías, badges de UI) compartidos entre miles de ítems del catálogo. |
| **Estructural** | **Proxy** | `src/LogiMarket.Application/Proxy/PagoAutorizadoProxy.cs` | Control estricto de autorización (RBAC) previo al procesamiento de cobros (solo roles ADMIN y VENTAS autorizados). |
| **Comportamental**| **Chain of Resp.** | `src/LogiMarket.Application/Chain/ValidadorPedidoChain.cs` | Cadena de validadores desacoplados: `ClienteActivo` → `LineasPedido` → `StockDisponible` → `PermisoConfirmacion`. |
| **Comportamental**| **Command** | `src/LogiMarket.Application/Command/ComandosConcretos.cs` | Encapsula acciones como objetos (`ConfirmarPedidoCommand`, etc.) ejecutados por `CommandInvoker` con historial. |
| **Comportamental**| **Interpreter** | `src/LogiMarket.Application/Interpreter/FiltroPedidosInterpreter.cs` | Parser léxico y evaluador de consultas booleanas sobre pedidos (soporta `AND`, `OR`, `=`, `>`, `<`, `>=`). |
| **Comportamental**| **Iterator** | `src/LogiMarket.Domain/Iterator/ComponenteFisicoIterator.cs` | Recorrido recursivo seguro y lineal de las hojas físicas de kits anidados evitando bucles cíclicos. |
| **Comportamental**| **Mediator** | `src/LogiMarket.Application/Mediator/FormularioPrincipalMediator.cs` | Coordina la navegación y refresco reactivo entre las 12 vistas modulares de WinForms sin acoplamiento cruzado. |
| **Comportamental**| **Memento** | `src/LogiMarket.Application/Memento/HistorialBorrador.cs` | Soporte completo de Deshacer/Rehacer (`Undo`/`Redo`) en la edición del carrito de compras. |
| **Comportamental**| **Observer** | `src/LogiMarket.Application/Observer/ObserverPattern.cs` | Publicación de eventos de dominio (`PedidoPagado`, `PedidoDespachado`) hacia múltiples suscriptores registrados. |
| **Comportamental**| **State** | `src/LogiMarket.Domain/State/PedidoEstados.cs` | Máquina de estados finita del pedido (`Borrador` → `Validado` → `Pagado` → `Despachado` → `Entregado` / `Cancelado`). |
| **Comportamental**| **Strategy** | `src/LogiMarket.Domain/Strategy/DescuentoStrategies.cs` | Algoritmos de descuento según categoría de cliente (`MayoristaDescuentoStrategy` 15%, `VipDescuentoStrategy` 10%, `SinDescuentoStrategy` 0%). |
| **Comportamental**| **Template Method**| `src/LogiMarket.Domain/TemplateMethod/DevolucionTemplates.cs` | Esqueleto invariante para procesamiento de devoluciones con pasos variables (`DevolucionPorDefecto`, `DevolucionPorArrepentimiento`). |
| **Comportamental**| **Visitor** | `src/LogiMarket.Domain/Visitor/Visitors.cs` | Operaciones sobre la jerarquía de catálogo sin modificar clases (`DesgloseComponentesVisitor`, `EstimacionPesoVisitor`). |

---

## 4. Requisitos del Sistema y Entorno de Ejecución

- **Sistema Operativo:** Windows 10 / Windows 11 (para ejecución de WinForms) o Linux/macOS (para ejecución de tests en consola).
- **SDK Requerido:** .NET 8.0 SDK (versión 8.0.100 o superior).
- **Motor de Base de Datos:** SQLite 3 (embebido mediante Microsoft.EntityFrameworkCore.Sqlite).
- **Herramientas recomendadas:** Visual Studio 2022 (v17.8+ con carga de trabajo de escritorio .NET) o Visual Studio Code con C# Dev Kit.

---

## 5. Instrucciones de Compilación y Ejecución

### 5.1 Restauración y Compilación
Abra una terminal de PowerShell o CMD en la carpeta raíz de la solución (`LogiMarket360/`) y ejecute:

```powershell
# Restaurar paquetes NuGet
dotnet restore LogiMarket360.sln

# Compilar toda la solución en modo Debug o Release
dotnet build LogiMarket360.sln --configuration Release
```

### 5.2 Ejecución de las pruebas automatizadas
Para ejecutar la suite completa de los casos P01 a P38 y las pruebas adicionales de integración:

```powershell
dotnet test tests/LogiMarket.Tests/LogiMarket.Tests.csproj --logger "console;verbosity=normal"
```

**Referencia para una ejecución exitosa:**
```text
Pruebas totales: 43
     Correcto: 43
 Con error:     0
   Omitido:     0
Tiempo total:  ~30 segundos (100% aprobado)
```

### 5.3 Ejecución del Aplicativo de Escritorio (WinForms)
Para iniciar la interfaz gráfica con las 12 vistas modulares:

```powershell
dotnet run --project src/LogiMarket.WinForms/LogiMarket.WinForms.csproj
```

*Nota: La primera vez que se inicia el sistema, el componente `DatabaseSeeder` aplica las migraciones de EF Core al archivo local `logimarket360-migrada.db` en la carpeta de ejecución y carga el catálogo inicial oficial, usuarios y clientes de prueba.*

---

## 6. Credenciales y Cuentas de Acceso (RBAC)

El sistema implementa control de acceso basado en roles (RBAC) aplicado en capa de aplicación (`PagoAutorizadoProxy`, comandos y validadores de permisos):

| Rol | Usuario Demo | Contraseña Demo | Permisos Asignados |
| :--- | :--- | :--- | :--- |
| **ADMIN** | `admin.demo` | `admin123` | Acceso total: Configuración, Gestión de Usuarios, Catálogo, Descuentos, Auditoría, Pagos y Despachos. |
| **VENTAS** | `ventas.demo` | `ventas123` | Clientes, Carrito, Creación y Confirmación/Cobro de Pedidos, Comprobantes de Venta. No puede despachar bodega. |
| **BODEGA** | `bodega.demo` | `bodega123` | Visualización de inventario, Despacho físico de pedidos PAGADOS, Confirmación de Entrega. No puede cobrar. |
| **CONSULTA**| `consulta.demo` | `consulta123` | Acceso de solo lectura al catálogo, stock y reportes. Rechazado automáticamente ante cualquier mutación o cobro. |

---

## 7. Catálogo Oficial y Reglas de Negocio Clave

### 7.1 Productos Simples (Terminales)
1. **P01 - Mouse Óptico USB:** USD 20.00 | Peso: 0.20 kg | Stock inicial: 8 unidades.
2. **P02 - Teclado Mecánico USB:** USD 30.00 | Peso: 0.70 kg | Stock inicial: 5 unidades.
3. **P03 - Memoria USB 64GB:** USD 10.00 | Peso: 0.10 kg | Stock inicial: 20 unidades.
4. **P04 - Cámara Web Full HD:** USD 40.00 | Peso: 0.35 kg | Stock inicial: 4 unidades.
5. **P05 - Alfombrilla:** USD 5.00 | Peso: 0.10 kg | Stock inicial: 30 unidades.

### 7.2 Kits Compuestos Anidados (Composite)
- **`KIT_OFICINA`:** P01 (Mouse) + P02 (Teclado) = **USD 50.00** | Peso total: 0.90 kg.
- **`KIT_TELETRABAJO`:** `KIT_OFICINA` + P03 (Memoria USB) = **USD 60.00** | Peso total: 1.00 kg.
- **Validación anti-ciclos:** Si se intenta agregar `KIT_TELETRABAJO` dentro de `KIT_OFICINA`, el dominio rechaza inmediatamente la operación con `InvalidOperationException`.

### 7.3 Reglas de Descuentos (Strategy)
1. **MAYORISTA (15%):** Aplica con máxima prioridad si el pedido contiene 10 o más unidades físicas totales.
2. **VIP (10%):** Aplica a clientes VIP cuando la compra no califica a volumen mayorista.
3. **NORMAL (0%):** Tarifa regular sin descuento.
- *Ejemplo de validación:* Ana (VIP) comprando 1 `KIT_TELETRABAJO` (USD 60.00) con tarifa ESTÁNDAR (USD 5.00): Subtotal $60.00 - Descuento 10% ($6.00) + Envío $5.00 = **USD 59.00**.

### 7.4 Políticas de Devolución (Template Method)
- Solo pedidos en estado **ENTREGADO** pueden ser objeto de devolución.
- **Flete no reembolsable:** La tarifa de envío no se reembolsa bajo ningún motivo.
- **DEFECTO:** Hasta 30 días de plazo para devolución total. Reembolso para Ana: $59.00 - $5.00 (flete) = **USD 54.00**.
- **ARREPENTIMIENTO:** Hasta 7 días calendario con ítem sin uso. Si excede 7 días, se rechaza formalmente.

---

## 8. Reset y Mantenimiento de la Base de Datos

Si desea reiniciar la base de datos a su estado prístino de fábrica:
1. Cierre el aplicativo WinForms si está en ejecución.
2. Respalde y retire el archivo de demostración `logimarket360-migrada.db` ubicado en el directorio de salida del proyecto `LogiMarket.WinForms`.
3. Al volver a iniciar la aplicación o correr las pruebas, la base de datos será recreada y repoblada automáticamente por el `DatabaseSeeder`.

La aplicación inicia en Acceso y aplica las migraciones pendientes a logimarket360-migrada.db.

### Procesamiento automático de Outbox

Al abrir la aplicación se procesan eventos pendientes y fallidos. El ciclo se repite cada 15 segundos y abre un contexto SQLite por ejecución. Las acciones de demostración y el observador comparten el mismo coordinador en la aplicación. Los avisos son simulados y usan el contacto del cliente asociado con el pedido.