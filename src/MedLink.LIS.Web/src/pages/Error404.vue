<template>
  <div class="fullscreen bg-grey-2 text-center q-pa-md flex flex-center">
    <div>
      <div style="font-size: 20vh" class="text-primary">404</div>
      <div class="text-h5 text-grey-8" style="opacity: .6">Сторінку не знайдено</div>
      <q-btn class="q-mt-xl" color="primary" unelevated to="/laboratory/dashboard" label="На дашборд" no-caps />
    </div>
  </div>
</template>

<script>
export default { name: 'Error404' };
</script>
