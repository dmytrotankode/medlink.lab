/**
 * Boot-файл входу (Auth:Mode=Oidc). Виконується до старту застосунку: читає /api/v1/lab/auth/config, обробляє повернення з Identity
 * і за відсутності сесії перенаправляє на вхід. У режимах None/Header нічого не змінює («Працюю як…»).
 */
import { initAuth, authState } from '../services/auth';

export default async ({ store }) => {
  const state = await initAuth();
  store.commit('context/SET_AUTH', { mode: state.mode, runAs: state.runAs, bridge: state.bridge, shellHints: state.shellHints, error: state.error });
};

export { authState };
