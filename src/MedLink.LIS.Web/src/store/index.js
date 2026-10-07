import Vue from 'vue';
import Vuex from 'vuex';

import context from './modules/context';
import laboratory from './modules/laboratory';
import dictionaries from './modules/dictionaries';

Vue.use(Vuex);

export default function (/* { ssrContext } */) {
  const Store = new Vuex.Store({
    modules: {
      context,
      laboratory,
      dictionaries
    },
    strict: process.env.DEV
  });

  return Store;
}
