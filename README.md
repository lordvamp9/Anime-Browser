# 🎬 vamp9 Anime Suite (Browser & Setup) 🚀
*Un Front-end de Escritorio Moderno para Automatización Multimedia en Entornos de Interoperabilidad (WSL/Windows).*

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue.svg)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/Framework-.NET%208%20WPF-5C2D91.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Esta suite de software proporciona una capa gráfica interactiva (GUI) nativa basada en C# y WPF (Windows Presentation Foundation) bajo .NET 8. El diseño e interfaz de usuario se inspiran en las estéticas retro/vintage de sistemas operativos como Windows Vista y Windows 7 (Aero Glass, Glassmorphism, y Y2K visual style). El sistema está diseñado exclusivamente para la gestión, ordenación y automatización local de flujos de trabajo multimedia, comunicándose de manera interoperable con subsistemas Linux a través de WSL (Windows Subsystem for Linux) y guardando los estados locales a través de una base de datos indexada en formato JSON.

## ✨ Novedades Destacadas
* 🌐 **Soporte Bilingüe (Español / Inglés)**: Ahora integra motores duales. Utiliza `ani-es` para búsquedas y contenido en español (mediante jkanime), o cambia con un clic al motor `ani-cli` oficial en inglés para mayor calidad y acceso global (mediante gogoanime/allanime).
* 🪟 **Interfaz Aero Glass**: Efectos de cristal pulido, sombras suaves y degradados responsivos que ofrecen una experiencia visual moderna, fluida y de máxima categoría.

La solución se divide en dos módulos principales:
* 🛠️ **vamp9.AnimeSetup**: Un asistente automatizado de configuración e instalación silenciosa que valida el entorno, las dependencias de WSL (incluyendo *yt-dlp*) y prepara la infraestructura necesaria sin intervención técnica del usuario.
* 📺 **vamp9.AnimeDashboard**: El panel de control y visor principal de flujos que maneja la búsqueda de anime local/remoto, gestión de listas de visualización y control directo de reproducción.

## 🏗️ Arquitectura y Requisitos de Interoperabilidad

El software aprovecha la interoperabilidad nativa de Windows y Linux mediante las siguientes llamadas de comunicación:
* Ejecución delegada de procesos mediante llamadas parametrizadas a `wsl.exe`.
* Sincronización y monitoreo asíncrono de procesos de reproducción locales a través de sockets y eventos del sistema operativo.
* Gestión de persistencia local mediante serialización estructurada en `anime_db.json`.

## ⚖️ Deslinde de Responsabilidad Legal (Disclaimer)

Este software es únicamente un cliente gráfico de interfaz y herramientas de automatización local. 

* **Sin Alojamiento ni Distribución**: Este software no aloja, no almacena, no distribuye ni transmite ningún archivo de video, audio o material multimedia protegido por derechos de autor.
* **Herramienta de Interfaz Pura**: Las capacidades de scraping y consulta son procesadas de forma efímera en la máquina local del usuario final utilizando dependencias externas configuradas bajo su propia responsabilidad.
* **Responsabilidad del Usuario**: El uso de este software para visualizar o interactuar con contenido multimedia de terceros se rige por los términos y condiciones de los proveedores de contenido y las leyes de propiedad intelectual de la jurisdicción del usuario final. Los autores y colaboradores de este proyecto no asumen responsabilidad alguna por infracciones de propiedad intelectual u otros usos indebidos de esta herramienta.

## 🤝 Créditos de Terceros

Este proyecto automatiza, interactúa y depende de herramientas desarrolladas de forma independiente por la comunidad de software libre. Se otorga la atribución correspondiente a los siguientes componentes:

* **ani-es**: Script CLI independiente desarrollado para la consulta de información multimedia en terminales compatibles con bash (Español).
  * Repositorio Oficial: [Zhuchii/ani-es](https://github.com/Zhuchii/ani-es)
* **ani-cli**: A cli tool to browse and play anime (Inglés).
  * Repositorio Oficial: [pystardust/ani-cli](https://github.com/pystardust/ani-cli)
* **MPV Player**: Reproductor multimedia de código abierto y alta eficiencia multiplataforma utilizado para renderizar las salidas de video locales.
  * Repositorio Oficial: [mpv-player/mpv](https://github.com/mpv-player/mpv)

*Nota: Ninguno de los archivos binarios o repositorios de estas dependencias externas está distribuido o incorporado de manera directa dentro del código fuente de este proyecto.*

## 📂 Repositorio Oficial

El código fuente de este proyecto y su documentación pública se encuentran disponibles en:
[lordvamp9/Anime-Browser](https://github.com/lordvamp9/Anime-Browser)

## 📄 Licencia

Este proyecto se distribuye bajo la Licencia MIT. Para obtener más información sobre el descargo de responsabilidad y las condiciones de copia, consulte el archivo LICENSE.
