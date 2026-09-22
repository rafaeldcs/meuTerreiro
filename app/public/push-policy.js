/* Shared pure policy, exercised with Node VM tests. No user information is persisted here. */
globalThis.TerreiroPush = Object.freeze({
  safePath(path) {
    // Accept only exact known routes; never resolve arbitrary URLs from a notification.
    if (typeof path !== 'string') return '/notificacoes';
    if (['/notificacoes','/membros', '/mensalidades','/recibos','/recebimentos','/comprovantes','/revisoes','/caixa','/despesas','/aprovacoes','/campanhas','/compras','/eventos','/tarefas','/pagamentos','/recorrencias','/estoque','/documentos','/manutencao','/acessos','/trocas'].includes(path) || /^\/(limpezas|recibos)\/[a-f0-9]{32}$/.test(path)) return path;
    return '/notificacoes';
  },
  payload(data) {
    return {
      title: 'Atualização no aplicativo', body: 'Abra o aplicativo para consultar.',
      id: typeof data?.id === 'string' && /^[a-f0-9]{64}$/.test(data.id) ? data.id : 'update',
      path: globalThis.TerreiroPush.safePath(data?.path)
    };
  }
});
