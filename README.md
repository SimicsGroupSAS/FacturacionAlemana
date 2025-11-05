# 📄 Facturación Alemana

Una aplicación de escritorio moderna desarrollada en **WPF (.NET 9)** para crear, visualizar y exportar facturas siguiendo el estándar alemán de facturación electrónica (factura en formato XML y PDF).

## 🎯 Características

- ✅ **Crear facturas desde cero** con detalles del vendedor y comprador
- ✅ **Cargar facturas existentes** desde archivos XML
- ✅ **Visualizar facturas** en vista previa antes de exportar
- ✅ **Exportar a PDF** con diseño profesional
- ✅ **Exportar a XML** en formato estándar alemán
- ✅ **Gestionar productos** en la factura (agregar, editar, eliminar)
- ✅ **Cálculo automático de impuestos y totales**
- ✅ **Interfaz moderna y responsive** con tema claro
- ✅ **Soporte para múltiples monedas**

## 📋 Requisitos previos

Antes de ejecutar la aplicación, asegúrate de tener instalado lo siguiente:

### Software requerido

- **Windows 10/11** o versión posterior (Sistema Operativo)
- **.NET 9 Runtime** o superior
  - [Descargar .NET 9](https://dotnet.microsoft.com/es-es/download/dotnet/9.0)
  - Verifica tu instalación con: `dotnet --version`

### Herramientas de desarrollo (opcional, solo para compilar desde código)

- **Visual Studio 2022** (versión Community o superior) o **Visual Studio Code**
- **SDK de .NET 9**

## 🚀 Instalación y configuración

### Opción 1: Ejecutar la aplicación compilada (Recomendado)

Si ya tienes el archivo ejecutable compilado (`.exe`):

1. Descarga o copia el archivo ejecutable a tu equipo
2. Haz doble clic en el archivo `.exe` para ejecutar la aplicación
3. ¡Listo! La aplicación se abrirá sin necesidad de configuración adicional

### Opción 2: Compilar desde el código fuente

Si deseas compilar la aplicación desde el código fuente:

1. **Clona o descarga el repositorio**
   ```powershell
   git clone <URL_DEL_REPOSITORIO>
   cd FacturacionAlemana
   ```

2. **Instala las dependencias de NuGet**
   ```powershell
   dotnet restore
   ```

3. **Compila el proyecto**
   ```powershell
   dotnet build
   ```

4. **Ejecuta la aplicación**
   ```powershell
   dotnet run --project FacturacionAlemana.csproj
   ```

### Opción 3: Usar Visual Studio

1. Abre `FacturacionAlemana.sln` en Visual Studio 2022
2. Haz clic derecho en el proyecto y selecciona **"Restaurar paquetes de NuGet"**
3. Presiona `F5` o selecciona **Depurar > Iniciar depuración**

## 💻 Cómo usar la aplicación

### Pantalla Principal

La aplicación consta de varias secciones accesibles desde el menú principal:

#### 1. **Página de inicio (Home)**
   - Opción para cargar una factura existente desde un archivo XML
   - Visualización rápida de facturas cargadas
   - Acceso directo a las otras funciones

#### 2. **Crear factura (Create Invoice)**
   - Rellena los datos del vendedor (empresa, dirección, contacto, etc.)
   - Ingresa los datos del comprador (igual información)
   - Agrega productos/servicios con cantidad y precio
   - El sistema calcula automáticamente:
     - Subtotal
     - Impuestos (según tasa configurada)
     - Total final
   - Selecciona la moneda (EUR, USD, GBP, etc.)
   - Guarda la factura en XML o PDF

#### 3. **Visualizar factura (Preview)**
   - Vista previa de la factura antes de exportar
   - Verficia todos los datos antes de generar documentos finales

#### 4. **Configuración (Settings)**
   - Personaliza los parámetros de la aplicación
   - Configura valores por defecto

### Flujo típico de uso

#### Crear una nueva factura:

1. Haz clic en la pestaña **"Crear factura"**
2. Completa la información del vendedor:
   - Nombre de la empresa
   - Persona de contacto
   - Departamento
   - Dirección (calle, código postal, ciudad)
   - País
   - Email
   - ID de impuestos (VAT/Steuer-ID)
3. Completa la información del comprador:
   - Nombre de la empresa
   - Persona de contacto
   - ID del comprador
   - Dirección, email, etc.
4. Agrega productos/servicios:
   - Haz clic en **"Agregar producto"**
   - Ingresa el nombre del producto
   - Especifica cantidad
   - Establece el precio unitario
   - Haz clic en **"Guardar"**
5. Define fechas:
   - Fecha de emisión (se rellena automáticamente con la fecha actual)
   - Fecha de vencimiento
6. Configura impuestos:
   - Selecciona la tasa de impuesto aplicable
7. Elige la moneda en la que se emite la factura
8. Haz clic en **"Generar factura"** para guardar como XML y/o PDF

#### Cargar una factura existente:

1. En la página de inicio, haz clic en **"Cargar XML"**
2. Selecciona un archivo XML de factura
3. La aplicación cargará y mostrará todos los detalles
4. Puedes hacer clic en **"Generar PDF"** para obtener una versión en PDF

### Gestión de productos

En la tabla de productos puedes:
- **Agregar**: Utiliza el botón "Agregar producto"
- **Editar**: Haz clic en la fila del producto para editarlo
- **Eliminar**: Selecciona la fila y usa la opción de eliminar
- **Ver resumen**: Los totales se actualizan automáticamente

## 📁 Estructura del proyecto

```
FacturacionAlemana/
├── Models/                    # Modelos de datos
│   ├── Factura.cs            # Clase principal de factura
│   └── Producto.cs           # Clase de producto
├── Services/                  # Servicios de negocio
│   ├── XmlGeneratorService.cs # Generador de XML
│   ├── XmlReaderService.cs    # Lector de XML
│   └── PdfGeneratorService.cs # Generador de PDF
├── Utils/                     # Utilidades
│   └── ...
├── Assets/                    # Recursos de la aplicación
│   ├── icono.ico             # Icono de la aplicación
│   └── plantilla.png         # Plantilla de logo
├── Ejemplos/                  # Ejemplos de facturas
│   ├── G25R-076.xml          # Ejemplo de XML de factura
│   ├── G25R-076.pdf          # Ejemplo de PDF de factura
│   └── ...
├── MainWindow.xaml           # Ventana principal
├── HomePage.xaml             # Página de inicio
├── CreateInvoicePage.xaml    # Página de crear factura
├── PreviewWindow.xaml        # Ventana de vista previa
├── SettingsPage.xaml         # Página de configuración
└── FacturacionAlemana.csproj # Archivo de proyecto
```

## 🔧 Dependencias

La aplicación utiliza las siguientes librerías .NET:

| Paquete | Versión | Descripción |
|---------|---------|-------------|
| **QuestPDF** | 2023.4.0 | Generación de PDF con fluent API |
| **System.Drawing.Common** | 7.0.0 | Funcionalidad de dibujo y gráficos |
| **WPF-UI** | 4.0.2 | Componentes de interfaz moderna para WPF |

Estas dependencias se instalan automáticamente mediante NuGet al compilar el proyecto.

## 🎨 Interfaz de usuario

- **Tema**: Claro (Light Theme)
- **Framework UI**: WPF con componentes modernos (WPF-UI)
- **Responsive**: Se adapta a diferentes resoluciones de pantalla
- **Idioma**: Interfaz en español

## 📤 Formatos de exportación

### Formato XML
- Sigue el estándar alemán de facturación electrónica
- Compatible con sistemas de facturación de la UE
- Incluye todos los detalles de vendedor, comprador y productos

### Formato PDF
- Documento profesional e imprimible
- Incluye logo de empresa
- Detalle completo de la factura
- Listo para enviar a clientes

## ⚙️ Configuración

Para acceder a la configuración:
1. Haz clic en la pestaña **"Configuración"**
2. Modifica los parámetros según tus necesidades
3. Los cambios se guardan automáticamente

## 🐛 Solución de problemas

### La aplicación no inicia
- **Verificación**: Asegúrate de tener .NET 9 Runtime instalado
- **Solución**: Instala .NET 9 desde https://dotnet.microsoft.com/es-es/download/dotnet/9.0

### Error al generar PDF
- **Verificación**: Comprueba que QuestPDF esté correctamente instalado
- **Solución**: Ejecuta `dotnet restore` y `dotnet build` nuevamente

### Archivo XML no se carga
- **Verificación**: Asegúrate de que el XML sea válido y siga el formato estándar
- **Solución**: Usa uno de los archivos de ejemplo en la carpeta `Ejemplos/`

### La interfaz se ve distorsionada
- **Solución**: Intenta cambiar la escala de pantalla en Windows o reinicia la aplicación

## 📧 Soporte

Si encuentras problemas o tienes sugerencias:
1. Revisa los ejemplos en la carpeta `Ejemplos/`
2. Consulta el archivo de logs (si está disponible)
3. Contacta al desarrollador o propietario del proyecto

## 📜 Licencia

Este proyecto está desarrollado para SIMICS Trading GmbH. Consulta el archivo de licencia para más detalles sobre los términos de uso.

## 🔐 Datos y privacidad

- Los archivos de factura se guardan localmente en tu equipo
- La aplicación no envía datos a servidores externos
- Todos los datos se procesan localmente para máxima privacidad

## 🚀 Características futuras

Posibles mejoras planeadas:
- [ ] Soporte para múltiples idiomas
- [ ] Integración con base de datos para histórico de facturas
- [ ] Envío de facturas por email automático
- [ ] Plantillas personalizadas
- [ ] Importación de clientes desde CSV
- [ ] Reportes y estadísticas

## 📞 Contacto

**Empresa**: SIMICS Trading GmbH

Para más información sobre facturación electrónica alemana, consulta:
- [Portal de facturación alemana](https://www.rechnungen-online.de/)
- [Estándar ZUGFeRD](https://www.zugferd.de/)

---

**Versión**: 1.0  
**Última actualización**: Noviembre 2025  
**Plataforma**: Windows 10/11 + .NET 9

¡Gracias por usar Facturación Alemana! 🎉
