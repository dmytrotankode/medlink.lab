/**
 * THIS FILE IS GENERATED AUTOMATICALLY.
 * DO NOT EDIT.
 *
 * You are probably looking on adding startup/initialization code.
 * Use "quasar new boot <name>" and add it there.
 * One boot file per concern. Then reference the file(s) in quasar.conf.js > boot:
 * boot: ['file', ...] // do not add ".js" extension to it.
 *
 * Boot files are your "main.js"
 **/

import lang from 'quasar/lang/uk'

import iconSet from 'quasar/icon-set/material-icons'


import Vue from 'vue'

import {Quasar,Notify,Dialog,Loading,LoadingBar} from 'quasar'


Vue.use(Quasar, { config: {"notify":{"position":"top-right","timeout":2500},"loadingBar":{"color":"accent","size":"3px"}},lang: lang,iconSet: iconSet,plugins: {Notify,Dialog,Loading,LoadingBar} })
