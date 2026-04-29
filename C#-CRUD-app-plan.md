# ** C# CRUD web app (support ticket system) **

## STACK
- C# web app framework => MVC
- SQLite/realny sql server ak skola poskytne -> bud raw queries alebo ORM, isiel by som radsej ORM => ef core
- HTML a CSS -> mozeme generovat pomocou AI
- JavaScript -> snad nebudeme potrebovat

## Endpointy

- / -> default landing page
- Register
- Log in => /auth/ controler, vsetky endpointy co potrebuju login pojdu cez tento endpoint 
- Log out
- /auth/:user_id/tickets/ -> vsetky tickety od daneho usera, rozdielne ked je niekto admin, mozno by nebolo odveci spravit tomu vlastny endpoint
- /auth/:user_id/ticketDetail -> detailne zobrazenie ticketu
- /auth/addTicket
- /auth/deleteTicket -> asi by stacilo len zmenit HTTP method v ticket alebo user/tickets endpointe
- /auth/updateTicket -> same as the above
- search => asi by tiez malo ist cez /auth

## Databaza
tab 1  `users`

| nazov    | datovy typ | constains            | poznamka                        |
|----------|------------|----------------------|---------------------------------|
| id       | text       | primary key not null | id musi byt UUID preto text     |
| email    | text       | not null unique      |                                 |
| heslo    | text       | not null             | iba SHA256 hash                 |
| is_admin | integer    |                      | sqlite nema bool ako datovy typ |


tab 2 `tickets`

| nazov       | datovy typ | constains            | poznamka                                                    |
| ----------- |------------|----------------------| ----------------------------------------------------------- |
| id          | integer    | primary key not null |                                                             |
| predmet     | text       | not null             |                                                             |
| vytvorny    | text       | not null             | datum v ISO formate kedy bol ticket vytvoreny               |
| typ         | text       | not nul              | bug report/pridanie feature/?otazka?                        |
| ticket text | text       | not null             |                                                             |
| zavaznost   | text       |                      |                                                             |
| obrazok?    | blob       |                      |                                                             |
| user_id     | text       | FK     not null      | cudzi kluc, ktory referencuje id v users,kto ho vytvoril    |
| asigned     | text       | FK                   | cuzdi kluc, referencuje id v users, ale user musi byt admin |

presnu strukturu cistime az v implementacii, ak by sme robili aj chat pri ticketoch tak bude treba este sposob ako ukladat historiu
`DB indexy` -> FK, maybe predmet kvoli vyhladavaniu, zbytok treba premysliet

## Poznamky
- Authorization -> session cookies, jednoduchsie jak BASIC alebo DIGEST
- ak by bol cas a chut teoreticky oAuth
- hesla budu v DB ukladane len ako hash cez SHA256 algoritmus
- na requesty pouzivat JSON alebo formy, ak C# neforcuje alebo nerobi lepsie z XML 
- kazdu DB operaciu treba dat do try/catch bloku

## Rozsirenia
- pridat moznost filtrovat tickety podla typu/zavaznosti/casu vytvorenia
- email notifikacie ked sa zmeni stav ticketu
- sifrovanie email v users tabulke pomocou AES
- moznost chatu v detailnom zobrazeni ticketu, ak by bolo fakt ze vela casu tak websocket/signalR inak neinteraktivny chat
-# csrf prevention (toto je uplne useless pre semestralny projekt co nikdy neodide z localhostu) 
