module.exports = {
  presets: [
    [
      '@quasar/babel-preset-app',
      {
        // core-js 3 (у package.json), а не 2 за замовчуванням пресету 1.x
        presetEnv: { corejs: 3 }
      }
    ]
  ]
};
