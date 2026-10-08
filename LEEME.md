# Baby Girlie

Tienda de ropa para niñas creada con ASP.NET Core 10 para Visual Studio 2026 y SQL Server. Incluye 30 prendas de demostración, filtros, carrito, pedidos y cuentas de clientes.

## Abrir en Visual Studio

1. Abrir `BabyGirlie.sln` con Visual Studio 2026 y la carga de trabajo «Desarrollo de ASP.NET y web».
2. Confirmar que .NET 10 y SQL Server LocalDB están instalados. La conexión está en `appsettings.json`; puede cambiarse para otra instancia SQL Server.
3. Compilar el proyecto. En una terminal de esta carpeta, ejecutar `dotnet bin/Debug/net10.0/BabyGirlie.dll --init-db`. Este paso crea o amplía la base `BabyGirlieDemo` y carga las 30 prendas sin borrar pedidos existentes. Repetirlo después de actualizar `catalog-seed.json`.
4. Ejecutar la aplicación con F5. La página local abre en `http://localhost:5186`.

## Tienda y cuentas

Las personas pueden registrarse con una dirección Gmail o de cualquier otro proveedor y una contraseña de al menos 8 caracteres. La contraseña se almacena como hash, no en texto. Después de iniciar sesión pueden registrar pedidos y ver solo los suyos en «Mi cuenta». Los pedidos anteriores a la incorporación de cuentas no se asignan automáticamente a ningún cliente.

Las cuentas y pedidos están en las tablas `Customers`, `Orders` y `OrderItems`. La sesión se mantiene con una cookie protegida para uso local. El formulario de pedidos obtiene precios y tallas de SQL Server; nunca confía en un total enviado desde el navegador.

**Antes de publicar para clientes reales:** configurar HTTPS, verificación de correo, recuperación de contraseña, políticas de privacidad y un servicio de correo para los mensajes de la tienda. «Continuar con Google» requiere conectar credenciales OAuth de una cuenta de Google; no está configurado en esta versión.

## Catálogo

`catalog-seed.json` contiene las 30 prendas de demostración. Sus fotos están en `wwwroot/assets` y son ilustrativas, no inventario real. El banner usa `banner-girl.jpg`, `banner-floral.jpg` y `banner-casual.jpg`; el catálogo muestra únicamente prendas sin modelos. Las imágenes nuevas se crearon con ImageGen con fotografía de producto sobre fondo claro, sin texto ni logos. Sustituir fotos, descripciones, tallas y precios por los reales antes de publicar.

## Pagos y operación

Los pedidos quedan **pendientes de confirmación**. No se cobra por tarjeta ni se envían mensajes automáticos. Para activar una pasarela se necesita la cuenta comercial, guardar sus claves privadas en el servidor, generar pagos con el total de SQL Server y verificar las notificaciones del proveedor. También falta un panel administrativo para gestionar stock y pedidos; por ahora los productos se actualizan desde `catalog-seed.json` y los pedidos pueden consultarse en SQL Server.

## Verificación

Se comprobó la compilación, el catálogo de 30 productos, búsqueda, filtros, carrito, registro con correo, inicio y cierre de sesión y pedidos asociados a cuentas. La prueba con dos clientes confirmó que uno no puede ver los pedidos del otro. La consulta de vulnerabilidades de NuGet mostró una advertencia de conectividad; no impidió compilar.
