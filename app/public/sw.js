/* Static-only offline support. Never cache pages containing user data or any API responses. */
importScripts('/push-policy.js');
const CACHE = 'terreiro-static-v1';
self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(['/offline.html','/icons/icon-192.png'])).then(() => self.skipWaiting()));
});
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith('terreiro-static-') && key !== CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim()));
});
self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET' || event.request.mode !== 'navigate' || new URL(event.request.url).origin !== self.location.origin) return;
  event.respondWith(fetch(event.request).catch(() => caches.match('/offline.html')));
});
self.addEventListener('push', event => {
  let data={};try{data=event.data?.json()||{};}catch{/* No unsafe text fallback. */}
  const payload=TerreiroPush.payload(data);
  event.waitUntil(self.registration.showNotification(payload.title,{
    body:payload.body,icon:'/icons/icon-192.png',badge:'/icons/badge.png',
    tag:payload.id,data:{path:payload.path},renotify:false
  }));
});
self.addEventListener('notificationclick', event => {
  event.notification.close();
  const url=new URL(TerreiroPush.safePath(event.notification.data?.path),self.location.origin).href;
  event.waitUntil(self.clients.matchAll({type:'window',includeUncontrolled:true}).then(async windows => {
    const existing=windows.find(client=>new URL(client.url).origin===self.location.origin);
    if(existing){await existing.navigate(url);return existing.focus();}
    return self.clients.openWindow(url);
  }));
});
self.addEventListener('message',event=>{
  if(event.data?.type==='LOGOUT') event.waitUntil(self.registration.getNotifications().then(all=>all.forEach(n=>n.close())));
});
