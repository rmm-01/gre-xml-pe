# Pantalla de la Guía de Remisión Electrónica (Angular)

Interfaz para la API de `src/GreXml.Api`. Ver el [README principal](../README.md).

## Cómo ejecutarla

Requisitos: Node.js 20 o superior y la API en ejecución (`dotnet run --project src/GreXml.Api --launch-profile http`).

```bash
cd web
npm install
npm start
```

Abrir `http://localhost:4200`. El servidor de desarrollo reenvía `/guias` y `/envios` a la API en
`http://localhost:5142` (`proxy.conf.json`), así que no hace falta configurar CORS.

## Pruebas

```bash
npm test
```
