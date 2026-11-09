<!--
  Умовна оболонка МедЛінк: імітує верх реальної МІС (логотип, головне меню, профіль/організація, вихід), у якій
  живе модуль «Лабораторія». Фронту МІС для розробки немає, тож це лише орієнтир для команди MedLink: у реальній системі
  цю смугу надає МІС, а наші екрани вбудовуються під нею. Деталі — docs/MEDLINK_SHELL_MAP.md.
  Вимикається LIS_SHELL_HINTS=0 (або Lab:ShellHints=false); у режимах None/Header/Oidc працює однаково.
-->
<template>
  <div v-if="enabled" class="medlink-shell" data-testid="medlinkShell">
    <div class="medlink-shell__logo" data-testid="shellLogo">
      <q-icon name="local_hospital" size="18px" />
      <span>МедЛінк</span>
      <q-tooltip>Заглушка логотипа МІС. Шапку й головне меню надає МІС.</q-tooltip>
    </div>

    <nav class="medlink-shell__menu" aria-label="Умовне головне меню МІС">
      <span v-for="m in stubItems" :key="m" class="medlink-shell__item medlink-shell__item--stub">
        {{ m }}
        <q-tooltip>Розділ МІС (заглушка)</q-tooltip>
      </span>
      <span class="medlink-shell__item medlink-shell__item--active" data-testid="shellLabTile">
        <q-icon name="fas fa-flask" size="12px" class="q-mr-xs" />Лабораторія
        <q-tooltip>Плитка модуля: відкриває {{ labUrl }} у тій самій вкладці (вхід через Identity МедЛінк).</q-tooltip>
      </span>
    </nav>

    <q-space />

    <label class="medlink-shell__toggle gt-sm" data-testid="shellHintsToggle">
      <q-toggle :value="hintsUser" dense size="xs" @input="setHints" />
      <span>Підписи вбудовування</span>
      <q-tooltip>Показувати значки «Ця частина належить МедЛінк» на екранах лабораторії</q-tooltip>
    </label>

    <div class="medlink-shell__user" data-testid="shellUser">
      <q-icon name="account_circle" size="20px" />
      <div class="column no-wrap" style="line-height: 1.1">
        <span class="ellipsis" style="max-width: 200px">{{ userName }}</span>
        <span class="medlink-shell__sub ellipsis" style="max-width: 200px">{{ profileLine }}</span>
      </div>
      <q-tooltip max-width="320px">
        Вибір профілю й організації — в МІС (перемикач профілів). {{ oidcMode ? 'Особа — з токена Identity (sub).' : 'Без входу: поточний співробітник з «Працюю як…».' }}
      </q-tooltip>
    </div>

    <q-btn flat dense no-caps icon="logout" label="Вийти" size="sm" class="medlink-shell__logout" data-testid="shellLogout" @click="logout">
      <q-tooltip>{{ oidcMode ? 'Вихід із Identity МедЛінк' : 'Без входу виходити нікуди: у реальній МІС тут завершення сесії' }}</q-tooltip>
    </q-btn>
  </div>
</template>

<script>
import { mapGetters, mapState } from 'vuex';
import { signOut, getTokenProfile } from '../../services/auth';
import { ROLE } from '../../utils/statuses';

export default {
  name: 'MedlinkShell',
  data () {
    return { stubItems: ['Пацієнти', 'Декларації', 'Прийоми', 'Направлення', 'Рецепти', 'Звіти'] };
  },
  computed: {
    ...mapGetters('context', ['oidcMode', 'currentEmployee', 'currentRole', 'labName']),
    ...mapGetters('context', { enabled: 'shellEnabled' }),
    ...mapState('context', ['hintsUser']),
    labUrl () { return process.env.VUE_ROUTER_BASE || '/'; },
    userName () {
      const t = this.oidcMode ? getTokenProfile() : null;
      const e = this.currentEmployee;
      return (t && t.name) || (e && (e.fullName || e.name)) || (t && t.email) || '—';
    },
    profileLine () {
      const role = ROLE[this.currentRole] || '';
      return [role, this.labName].filter(Boolean).join(' · ');
    }
  },
  methods: {
    setHints (v) { this.$store.commit('context/SET_HINTS_USER', v); },
    logout () {
      if (this.oidcMode) signOut();
      else this.$q.notify({ type: 'info', message: 'Режим без входу: у реальній МІС тут завершується сесія користувача.' });
    }
  }
};
</script>

<style lang="stylus">
.medlink-shell
  display flex
  align-items center
  gap 14px
  min-height 32px
  padding 0 12px
  background #2b3a4a
  color #e8eef4
  font-size 12.5px
  border-bottom 1px dashed #7d8da0
  &__logo
    display flex
    align-items center
    gap 5px
    font-weight 700
    letter-spacing .3px
  &__menu
    display flex
    gap 2px
    overflow hidden
  &__item
    padding 6px 9px
    border-radius 3px
    white-space nowrap
    &--stub
      color #aebccb
    &--active
      background #1565c0
      color #fff
      font-weight 600
  &__toggle
    display flex
    align-items center
    gap 4px
    cursor pointer
    color #aebccb
    font-size 11.5px
  &__user
    display flex
    align-items center
    gap 6px
  &__sub
    font-size 10.5px
    color #aebccb
  &__logout
    color #e8eef4
@media (max-width: 1100px)
  .medlink-shell__menu .medlink-shell__item--stub
    display none
</style>
