# Teclado con mouse

Teclado flotante para Windows para escribir solo con el mouse, deslizando sobre las letras como en Gboard/Swype.
Escribe directamente en la app que tengas abierta (navegador, Word, WhatsApp, Discord…) sin robarle el foco.

## Uso

1. Compilar y abrir:
   ```
   dotnet build TecladoConMouse -c Release
   TecladoConMouse\bin\Release\net10.0-windows\TecladoConMouse.exe
   ```
   Queda en la bandeja del sistema (ícono celeste). Desde su menú: *Iniciar con Windows*, configuración y salir.
2. Hacé clic en el campo donde querés escribir.
3. Abrí el teclado con **Ctrl+Alt+K** (o clic en el ícono de la bandeja). Aparece debajo del cursor.
4. **Deslizá** de letra en letra sin soltar el clic y soltá al final de la palabra. Se escribe la palabra más probable con un espacio.

| Acción | Cómo |
|---|---|
| Palabra | Deslizar sobre las letras |
| Letra suelta | Clic en la tecla (aparecen sugerencias arriba) |
| Cambiar la palabra por otra sugerencia | Clic en la sugerencia, o **tilt de la rueda** izquierda/derecha |
| Borrar | **Clic derecho** en el teclado, **botón lateral de atrás** o ⌫. Después de deslizar, borra la palabra entera |
| Borrar la palabra completa | **Doble clic derecho** en el teclado |
| Aceptar sugerencia / espacio | **Botón lateral de adelante** |
| Enter | **Clic de la rueda** en el teclado, o ⏎ |
| Mayúscula | ⇧ una vez = próxima letra, dos veces = bloqueo. Después de `. ? !` se activa sola |
| Números y símbolos | `123` |
| Mover el teclado | Arrastrar desde ⠿ |

Los botones laterales y el tilt se toman solo mientras el teclado está visible. El resto del tiempo funcionan normal.

## Configurar botones del G502 en G Hub

- **Abrir el teclado:** G502 → **Asignaciones** → pestaña **Teclas**, escribí la combinación `Ctrl+Alt+K` y arrastrala al botón
  que quieras (por ejemplo G9, el que está detrás de la rueda).
  Otra opción: pestaña **Sistema** → **Iniciar aplicación**, apuntando a `TecladoConMouse.exe`. Si la app ya está abierta, eso alterna el teclado.
- **Espacio en el botón del pulgar (G6):** asignale la función **Adelante**. Mientras el teclado está abierto funciona como espacio.

## Aprendizaje

- Las palabras que usás suben de prioridad.
- Las palabras que no están en el diccionario, escritas letra por letra, **no se guardan solas** (para no guardar contraseñas).
  Después de escribirlas aparece `＋ guardar «palabra»` en la barra de sugerencias.
- Todo queda en `%APPDATA%\TecladoConMouse\palabras_usuario.txt`.

### Entrenar con un chat de WhatsApp

Exportá el chat desde WhatsApp (*Exportar chat → Sin archivos*) y corré:

```
dotnet run --project TecladoConMouse.Entrenador -c Release -- "ruta\al\_chat.txt" "Tu nombre"
```

Sin el nombre, muestra los participantes del chat. Se cuentan solo tus mensajes (sin links, fotos, audios ni mensajes automáticos).
Las palabras que no están en el diccionario entran si las usaste 3 veces o más. El resultado va a
`%APPDATA%\TecladoConMouse\vocabulario_personal.txt` y se carga al abrir el teclado. Para deshacerlo, borrá ese archivo.

Para medir la mejora: `dotnet run --project TecladoConMouse.Pruebas -c Release -- vocabulario`.

## Configuración

`%APPDATA%\TecladoConMouse\configuracion.json` (reiniciar la app para aplicar):

- `AnchoTecla`, `AltoTecla`: tamaño de las teclas en píxeles.
- `Opacidad`: de 0 a 1.
- `AtajosAbrir`: atajos para abrir/cerrar, por ejemplo `"Ctrl+Alt+K"`, `"F13"`.
- `AtajoAceptar`, `AtajoBorrar`, `AtajoEnter`, `AtajoSiguienteSugerencia`: atajos extra mientras está visible.
- `BotonesLaterales`, `InclinacionRueda`: usar botones laterales / tilt mientras está visible.
- `OcultarDespuesDeEnter`: ocultar el teclado después de Enter (cómodo para búsquedas).

Abrir el `.exe` con `--mostrar` muestra el teclado al arrancar.

## Limitaciones

- No puede escribir en ventanas que corren como administrador salvo que la app también se ejecute como administrador.

## Desarrollo

`TecladoConMouse.Pruebas` prueba la lógica de escritura y mide la precisión del reconocedor con deslizamientos simulados:

```
dotnet run --project TecladoConMouse.Pruebas -c Release                    # pruebas + precisión
dotnet run --project TecladoConMouse.Pruebas -c Release -- ajustar         # buscar mejores pesos
dotnet run --project TecladoConMouse.Pruebas -c Release -- depurar rueda   # ver candidatas de una palabra
```

Diccionario: `es_50k.txt` de [hermitdave/FrequencyWords](https://github.com/hermitdave/FrequencyWords) (CC-BY-SA 4.0).
