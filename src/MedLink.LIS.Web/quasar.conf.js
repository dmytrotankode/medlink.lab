/* eslint-env node */
/*
 * MedLink LIS 4.0 — конфігурація Quasar CLI v1 (webpack 4), як у evomis/src/App.View.
 * Node 22: NODE_OPTIONS=--openssl-legacy-provider задається у npm-скриптах (cross-env).
 */

module.exports = function (ctx) {
  return {
    supportTS: false,

    boot: [
      'fonts',
      'axios',
      'oidc',
      'components',
      'guide'
    ],

    css: [
      'app.styl'
    ],

    // Шрифт Source Sans Pro підключається локально (boot/fonts.js, пакет @fontsource/source-sans-pro) — без CDN.
    extras: [
      'fontawesome-v5',
      'material-icons'
    ],

    framework: {
      iconSet: 'material-icons',
      lang: 'uk',
      all: 'auto',
      components: [],
      directives: [],
      plugins: [
        'Notify',
        'Dialog',
        'Loading',
        'LoadingBar'
      ],
      config: {
        notify: { position: 'top-right', timeout: 2500 },
        loadingBar: { color: 'accent', size: '3px' }
      }
    },

    build: {
      vueRouterMode: 'history',
      distDir: 'dist/spa',
      // Розгортання в підпапці (напр. /lab/): LIS_PUBLIC_PATH=/lab/ — шляхи ресурсів, база роутера та API
      publicPath: process.env.LIS_PUBLIC_PATH || '/',
      env: {
        API_BASE: JSON.stringify(process.env.API_BASE || ((process.env.LIS_PUBLIC_PATH || '/').replace(/\/$/, '') + '/api/v1/lab'))
      },
      extendWebpack (cfg) {
        cfg.resolve.alias = {
          ...cfg.resolve.alias,
          '@': require('path').resolve(__dirname, './src'),
          // MedLink Guide SDK — окрема бібліотека поруч (src/MedLink.Guide.Web), без залежності від Quasar
          '@guide': require('path').resolve(__dirname, '../MedLink.Guide.Web/src')
        };
        // SDK лежить поза коренем проєкту: дозволяємо резолвити core-js/runtime з node_modules вебу
        cfg.resolve.modules = (cfg.resolve.modules || ['node_modules']).concat([require('path').resolve(__dirname, 'node_modules')]);
      }
    },

    devServer: {
      https: false,
      port: 8080,
      open: false,
      proxy: {
        '/api': {
          target: 'http://localhost:5055',
          changeOrigin: true
        },
        '/health': {
          target: 'http://localhost:5055',
          changeOrigin: true
        },
        '/verify': {
          target: 'http://localhost:5055',
          changeOrigin: true
        }
      }
    },

    animations: [],

    ssr: { pwa: false },
    pwa: {},
    cordova: {},
    capacitor: {},
    electron: {}
  };
};
