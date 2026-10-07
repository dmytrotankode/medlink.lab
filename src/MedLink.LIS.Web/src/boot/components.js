/**
 * Глобальна реєстрація спільних компонентів та фільтрів форматування.
 */
import StatusChip from '../components/common/StatusChip.vue';
import FlagMarker from '../components/common/FlagMarker.vue';
import PageHeader from '../components/common/PageHeader.vue';
import ApiErrorBanner from '../components/common/ApiErrorBanner.vue';
import EmptyState from '../components/common/EmptyState.vue';
import BarcodeSvg from '../components/common/BarcodeSvg.vue';
import ConfirmDialog from '../components/common/ConfirmDialog.vue';
import { formatDate, formatDateTime, formatNumber, formatMoney, formatPercent, formatMinutes } from '../utils/format';

export default ({ Vue }) => {
  Vue.component('StatusChip', StatusChip);
  Vue.component('FlagMarker', FlagMarker);
  Vue.component('PageHeader', PageHeader);
  Vue.component('ApiErrorBanner', ApiErrorBanner);
  Vue.component('EmptyState', EmptyState);
  Vue.component('BarcodeSvg', BarcodeSvg);
  Vue.component('ConfirmDialog', ConfirmDialog);

  Vue.filter('date', formatDate);
  Vue.filter('datetime', formatDateTime);
  Vue.filter('num', formatNumber);
  Vue.filter('money', formatMoney);
  Vue.filter('pct', formatPercent);
  Vue.filter('minutes', formatMinutes);
};
