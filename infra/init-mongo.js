// Runs authenticated, in the private Docker network. Never publishes the database port.
try { rs.status(); }
catch (error) {
  if (error.code !== 94) throw error;
  rs.initiate({_id:'rs0',members:[{_id:0,host:'mongo:27017'}]});
}
let ready=false;
for(let i=0;i<90;i++){if(db.hello().isWritablePrimary){ready=true;break;}sleep(1000);}
if(!ready)throw new Error('Replica set did not become writable.');
const appdb=db.getSiblingDB(process.env.MONGO_DATABASE);
if(!process.env.MONGO_APP_PASSWORD)throw new Error('Application password is required.');
if(!appdb.getUser('terreiro_app')){
  appdb.createUser({user:'terreiro_app',pwd:process.env.MONGO_APP_PASSWORD,roles:[{role:'readWrite',db:process.env.MONGO_DATABASE}]});
}
print('Replica set ready; application user provisioned.');
