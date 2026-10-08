# Baby Girlie

Tienda de demostración de ropa para niñas, desarrollada con ASP.NET Core 10 y SQL Server para Visual Studio 2026. Incluye 30 prendas de muestra, carrito, registro con correo electrónico y pedidos asociados a cada cliente.

## Ejecutar en Visual Studio

Abre `BabyGirlie.sln`, compila el proyecto y prepara la base de datos con:

```powershell
dotnet bin/Debug/net10.0/BabyGirlie.dll --init-db
```

Después inicia el proyecto con F5. La conexión local predeterminada usa SQL Server LocalDB. Consulta [LEEME.md](LEEME.md) para más detalles.

## Publicación

El repositorio guarda el código; la tienda completa necesita un servidor ASP.NET Core y una base SQL Server accesible desde ese servidor. La cadena de conexión debe configurarse como variable de entorno `ConnectionStrings__Store` en el alojamiento, sin subir credenciales a GitHub. Ejecuta `--init-db` una vez en el entorno de destino para crear las tablas y cargar el catálogo.

Azure App Service con Azure SQL Database es una opción compatible. Antes de aceptar compradores reales, configura HTTPS, verificación de correo, recuperación de contraseña, información legal y fotos, precios e inventario reales. La pasarela de pagos todavía no está conectada; los pedidos quedan pendientes de confirmación.

GitHub Pages no ejecuta la aplicación ASP.NET Core ni SQL Server y no se usará como alojamiento de esta tienda.
