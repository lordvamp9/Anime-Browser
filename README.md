# vamp9 Anime Suite (Browser & Setup)
*Un Front-end de Escritorio Moderno para Automatización Multimedia en Entornos de Interoperabilidad (WSL/Windows).*

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-5C2D91.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Esta suite de software proporciona una interfaz gráfica nativa basada en C# y WPF (.NET 8) con un diseño inspirado en la estética Aero Glass. Facilita la búsqueda y reproducción local de anime a través de la interoperabilidad con subsistemas Linux en WSL.

## Características y Módulos
* **vamp9.AnimeSetup**: Asistente de instalación automatizado que configura el entorno WSL, instala dependencias (como yt-dlp) y prepara MPV de forma silenciosa.
* **vamp9.AnimeDashboard**: Panel principal de búsqueda y reproducción.

## Arquitectura e Interoperabilidad
* Comunicación delegada mediante llamadas de procesos a `wsl.exe`.
* Sincronización asíncrona de procesos de reproducción con MPV.
* Persistencia local mediante serialización en `anime_db.json`.

## Deslinde de Responsabilidad Legal
* **Sin Alojamiento**: Este software no aloja, almacena, distribuye ni transmite ningún archivo multimedia.
* **Interfaz Pura**: Las consultas y el scraping son procesados localmente mediante dependencias externas configuradas bajo responsabilidad del usuario.
* **Responsabilidad del Usuario**: El uso de esta herramienta queda sujeto a las leyes locales de propiedad intelectual y los términos de servicio de los proveedores de contenido.

## Créditos de Terceros
* **ani-es**: Script CLI para búsqueda de anime en español. [Zhuchii/ani-es](https://github.com/Zhuchii/ani-es)
* **ani-cli**: Script CLI para búsqueda de anime en inglés. [pystardust/ani-cli](https://github.com/pystardust/ani-cli)
* **MPV Player**: Reproductor multimedia local de código abierto. [mpv-player/mpv](https://github.com/mpv-player/mpv)

## Repositorio y Licencia
El código fuente está disponible en [lordvamp9/Anime-Browser](https://github.com/lordvamp9/Anime-Browser) bajo la Licencia MIT.
