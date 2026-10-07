import laboratoryRoutes from './laboratoryRoutes';
import portalRoutes from './portalRoutes';

const routes = [
  { path: '/', redirect: '/laboratory/dashboard' },
  ...laboratoryRoutes,
  ...portalRoutes,
  {
    path: '/verify/:token',
    name: 'verify',
    meta: { title: 'Перевірка бланка' },
    component: () => import('pages/portal/VerifyReport.vue')
  },
  {
    path: '*',
    component: () => import('pages/Error404.vue')
  }
];

export default routes;
