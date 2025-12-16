## 🏆 Top Contributors
![Contributors List](https://github.com/Anonymous0n3/wea-semworks/blob/master/contributors.svg)

Pro rozjetí aplikace v lokálním prostředí je třeba podniknout tyto kroky.

1. naklonujte repozitář

2. přepněte na větev Produkce

3. pro správnou funkčnost programu je třeba v kořenovém adresáři projektu vytvořit .env soubor

   a. Pokud systém běží na systému Linux je třeba ho vložit do adresáře celého programu a v docker-compose odkomentovat řádky env_file a - .env
   
   b. Na systému windows je třeba soubor umístit do adrešáře WebApplication1
   
4. do .env souboru je třeba nastavit následující proměnné

  a. Weather API proměnné - je třeba se zaregistrovat na https://www.weatherapi.com a hodnoty zkopírovat odsud
  
  WEATHER_API_KEY 
  
  WEATHER_API_URL
  
  
  b. SWOP proměnné - jako u předchozího kroku ale na jiné adrese https://swop.cx
  
  SWOP_API_KEY
  
  SWOP_API_URL


  c. Backend proměnné - pokud chcete změnit hodnoty tak upravte v docker-compose názvy a porty a změňte v .env
  BACKEND_BASE_URL=http://webapplication1:8003


  d. JWT proměnné - JWT je třeba vyplnit manuálně, JWT_KEY je heslo které používá server k šifrování, JWT_ISSUER je jméno serveru (jméno zvolte jakkékoliv chcete), JWT_AUDIENCE je stejná jako u serveru ale pro klinety obecně, JWT_EXPIREMINUTES určuje jak dlouho je uživetel přihlášen než je automatciky odhlášen
  
  JWT_KEY
  
  JWT_ISSUER
  
  JWT_AUDIENCE
  
  JWT_EXPIREMINUTES


  e. Google Login proměnné - Zde je třeba se zaregistrovat na Google Cloud Console a vytvořit si projekt, nastavení přímo je jedno ale je třeba googlu dát vědět na jakou URL se vracet, toto bude adresa serveru, pokud je pouze lokálně tak stačí localhost pokud běží na serveru je třeba url serveru
  
  GOOGLE_CLIENTID
  
  GOOGLE_CLIENTSECRET
  
  GOOGLE_REDIRECTURI


  f. MQTT proměnné - zde je třeba zadat údaje pro připojení k MQTT brokerovy
  
  MQTT_USERNAME
  
  MQTT_PASSWORD
  
  MQTT_ADDRESS
  
  MQTT_PORT
  
5.následně stačí v kořenové složce projektu udělat docker-compose up --build a aplikace se na serveru spustí
