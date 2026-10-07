/**
 * Кабінет пацієнта (/portal). Без OTP: пацієнт обирається зі списку (у бойовому evomis — із сесії).
 */
export default [
  {
    path: '/portal',
    component: () => import('layouts/portalLayout/PortalLayout.vue'),
    children: [
      { path: '', name: 'portal-home', meta: { title: 'Кабінет пацієнта' }, component: () => import('pages/portal/PortalHome.vue') },
      { path: ':patientId/orders', name: 'portal-orders', meta: { title: 'Мої дослідження' }, component: () => import('pages/portal/PortalOrders.vue'), props: true },
      { path: ':patientId/orders/:id', name: 'portal-order', meta: { title: 'Результат дослідження' }, component: () => import('pages/portal/PortalOrderResult.vue'), props: true },
      { path: ':patientId/trend/:testCode', name: 'portal-trend', meta: { title: 'Динаміка показника' }, component: () => import('pages/portal/PortalTrend.vue'), props: true },
      { path: ':patientId/notifications', name: 'portal-notifications', meta: { title: 'Сповіщення' }, component: () => import('pages/portal/PortalNotifications.vue'), props: true }
    ]
  }
];
