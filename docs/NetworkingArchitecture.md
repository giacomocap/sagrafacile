Ok, capisco perfettamente la situazione "da sagra"! Budget contenuto, tante postazioni e la necessità di far funzionare tutto in modo affidabile. Cerchiamo di mettere insieme un piano.

**Filosofia di Base:**

1.  **Rete Cablata dove Possibile:** Più stabile e veloce.
2.  **Wi-Fi Mirato:** Per i camerieri e postazioni dove il cavo è impraticabile.
3.  **Segmentazione Semplice:** Un'unica rete (subnet) per facilitare la comunicazione tra tutti i dispositivi e il server.
4.  **Affidabilità e Semplicità:** Componenti facili da configurare e gestire.

**Schema di Rete Proposto:**

```
                                     INTERNET
                                         |
                                +-------------------+
                                |   MODEM/ROUTER    | (Del fornitore di connettività, con Wi-Fi per Cassa/Bar)
                                |  (es. 192.168.1.1)|
                                +--------|----------+
                                         |
                                +-------------------+
                                |  SWITCH GIGABIT   | (Cassa - Switch Principale)
                                |      (S1)         |
                                +--------|----------+
                                         |
  +--------------------------------------+-------------------------------------------+
  |               |          |                  |                  |                  |
Server       PC Cassa 1  PC Cassa 2      PC Cassa 3      PC Coda Schermo   [Opzionale: NanoStation TX]
(192.168.1.10)                                                              (se ponte radio parte da qui)
                                                                                   |
                                                                                   | (Cavo Ethernet Cat 6, 60m)
                                                                                   |
                                                                        +-------------------+
                                                                        |  SWITCH GIGABIT   | (Capannoni - Switch Secondario)
                                                                        |      (S2)         |
                                                                        +--------|----------+
                                                                                 |
                                                    +----------------------------+----------------------------+
                                                    |                                                         |
                                        +-------------------+                                     +-------------------+
                                        | ACCESS POINT Wi-Fi|                                     | ACCESS POINT Wi-Fi|
                                        |      (AP1)        | (per camerieri Capannone 1)         |      (AP2)        | (per camerieri Capannone 2)
                                        +-------------------+                                     +-------------------+
                                              (Wi-Fi per telefoni camerieri)                            (Wi-Fi per telefoni camerieri)


Collegamento BAR (vicino Cassa):
PC Bar Cassa  <---- Wi-Fi ----> Modem/Router (Cassa) o AP dedicato se segnale debole


Collegamento PANINOTECA (30m, no cavo):

Opzione A (Ponte da Cassa):
Modem/Router o Switch S1 (Cassa) ----> NanoStation TX (Cassa, puntata verso Paninoteca)
                                            ((( Wireless Bridge )))
NanoStation RX (Paninoteca) ----> SWITCH GIGABIT (S3 - Paninoteca) ----> 2x PC Cassa Paninoteca
                                                                     ----> PC Ordini Pronti Paninoteca

Opzione B (Ponte da Capannone, se più comodo per linea d'aria):
Switch S2 (Capannoni) ----> NanoStation TX (Capannone, puntata verso Paninoteca)
                                            ((( Wireless Bridge )))
NanoStation RX (Paninoteca) ----> SWITCH GIGABIT (S3 - Paninoteca) ----> 2x PC Cassa Paninoteca
                                                                     ----> PC Ordini Pronti Paninoteca
```

**Componenti Dettagliati e Consigli:**

1.  **Cavo Ethernet (Cassa -> Capannoni, ~60m):**
    *   **Tipo:** **Cat 6 U/FTP (o F/UTP) con conduttori in rame solido 100%.**
    *   **Perché:** Buon bilanciamento tra prestazioni, costo e protezione dalle interferenze, sufficiente per 1 Gbps su quella distanza.

2.  **Router Principale (Cassa):**
    *   Utilizza quello fornito dal tuo ISP (TIM, Vodafone, etc.). Assicurati che abbia porte Gigabit Ethernet e un Wi-Fi decente (almeno Wi-Fi 5 - 802.11ac). Questo gestirà l'accesso a Internet, farà da DHCP server (assegna IP) e da gateway.
    *   **Configurazione Router Principale (DHCP):**
        *   **Indirizzo IP Router:** Solitamente `192.168.1.1` (o `192.168.0.1`). Questo sarà il **Gateway** per tutti i dispositivi.
        *   **Range DHCP:** Configura il router per assegnare indirizzi IP dinamici in un intervallo specifico, ad esempio da `192.168.1.50` a `192.168.1.200`. Questo lascia spazio per IP statici al di fuori di questo range.
        *   **DNS:** Il router stesso (`192.168.1.1`) può fungere da server DNS per la rete locale, oppure puoi usare DNS pubblici come `8.8.8.8` (Google) o `1.1.1.1` (Cloudflare).

3.  **Switch (S1 - Cassa, S2 - Capannoni, S3 - Paninoteca):**
    *   **Tipo:** **Switch Gigabit Ethernet non gestiti (unmanaged).**
    *   **Marche Economiche e Affidabili:** TP-Link, Netgear, D-Link.
    *   **Porte:**
        *   S1 (Cassa): Almeno 8 porte (1 per router ISP, 1 server, 3 PC cassa, 1 PC coda, 1 per cavo ai capannoni, 1 per NanoStation TX se parte da qui). Un 8 porte potrebbe bastare, un 16 porte dà più margine.
        *   S2 (Capannoni): Almeno 5 porte (1 da Cassa, 2 per AP, 1 per NanoStation TX se parte da qui). Un 5 o 8 porte.
        *   S3 (Paninoteca): Almeno 5 porte (1 da NanoStation RX, 2 PC cassa, 1 PC ordini). Un 5 o 8 porte.
    *   **Perché non gestiti:** Più economici e semplici (plug-and-play) per questa applicazione.

4.  **Access Point Wi-Fi (AP1, AP2 - Capannoni):**
    *   **Opzione Budget Pro:** **Ubiquiti UniFi AP AC Lite** o **TP-Link EAP225/EAP245 (Omada).**
        *   Pro: Ottime prestazioni, gestibili centralmente (anche se per 2 AP non è strettamente necessario usare il controller, si configurano singolarmente via app/web), affidabili, PoE (Power over Ethernet - alimentati tramite cavo di rete se lo switch S2 è PoE, altrimenti serve l'iniettore PoE incluso).
        *   Contro: Leggermente più costosi di opzioni consumer.
    *   **Opzione Super Budget:** **Router Wi-Fi consumer impostati in modalità Access Point.**
        *   Esempio: TP-Link Archer C6/A6 o simili.
        *   Pro: Molto economici.
        *   Contro: Prestazioni e copertura potrebbero essere inferiori, gestione meno raffinata. Vanno configurati disabilitando il DHCP e collegando il cavo proveniente dallo switch S2 a una porta LAN (non WAN) del router-AP. Assegna loro IP statici (es. 192.168.1.2, 192.168.1.3).
    *   **Configurazione Wi-Fi (per AP):**
        *   **SSID (Nome Rete):** Uguale per entrambi gli AP (es. "SAGRA_STAFF") per permettere il roaming dei camerieri.
        *   **Password:** WPA2/WPA3 robusta.
        *   **Canali:** Usa canali diversi e non sovrapposti per minimizzare interferenze (es. AP1 su canale 1, AP2 su canale 6 o 11 per la banda 2.4GHz; per la 5GHz scegli canali distanti).

5.  **Ponte Radio Wireless (Cassa/Capannoni <-> Paninoteca):**
    *   **Dispositivi:** **2 x Ubiquiti NanoStation Loco M5** (più datate, economiche, 5GHz, fino a 150Mbps) o **NanoStation 5AC Loco** (più recenti, più veloci, 5GHz, fino a 450Mbps). Per le tue esigenze (PC cassa e ordini), anche le Loco M5 sono più che sufficienti e costano meno.
    *   **Perché:** Affidabili, studiate per link punto-punto, resistenti all'esterno (se montate correttamente).
    *   **Configurazione:**
        *   Una NanoStation (TX - trasmittente) in modalità "Access Point PtP (Point-to-Point)".
        *   L'altra NanoStation (RX - ricevente) in modalità "Station PtP".
        *   Devono "vedersi" direttamente (linea d'aria libera da ostacoli).
        *   Imposta un canale fisso sulla 5GHz, SSID e password WPA2 dedicati solo a questo link.
        *   Assegna IP statici (es. Nano TX: 192.168.1.4, Nano RX: 192.168.1.5).
        *   Collega la Nano TX a uno switch (S1 o S2) e la Nano RX allo switch S3 della paninoteca.

6.  **PC Bar:**
    *   Si collegherà in Wi-Fi al modem/router della cassa. Se il segnale è debole, potresti considerare un piccolo ripetitore Wi-Fi consumer o, meglio, un terzo AP economico vicino alla cassa dedicato a questo.

**Configurazione Generale Rete e Indirizzamento IP:**

Una corretta pianificazione degli indirizzi IP è cruciale per la stabilità e l'accessibilità di SagraFacile.

*   **Gateway Predefinito e Server DNS:** Per tutti i dispositivi client (PC, telefoni, AP), l'indirizzo IP del router principale (es. `192.168.1.1`) sarà il gateway predefinito e spesso anche il server DNS primario.
*   **Server SagraFacile (Host Docker):**
    *   **IP Statico Obbligatorio:** Assegna un indirizzo IP statico alla macchina che ospiterà Docker e quindi SagraFacile. Ad esempio: `192.168.1.10`.
    *   **Subnet Mask:** Tipicamente `255.255.255.0` per una rete `/24`.
    *   **Perché Statico?** Tutti i client (casse, KDS, telefoni camerieri, display pubblici) dovranno puntare a questo indirizzo IP fisso per accedere all'applicazione SagraFacile. Se l'IP del server cambiasse (come potrebbe accadere con DHCP), i client non riuscirebbero più a connettersi.
*   **Altri Dispositivi Fissi (PC Cassa, Stampanti di Rete, KDS, AP, NanoStation):**
    *   È consigliabile assegnare indirizzi IP statici anche a questi dispositivi per una gestione più semplice e per evitare problemi nel caso il server DHCP avesse malfunzionamenti. Scegli IP al di fuori del range DHCP, ad esempio:
        *   PC Cassa 1: `192.168.1.20`
        *   PC Cassa 2: `192.168.1.21`
        *   Stampante Rete Cucina: `192.168.1.30`
        *   Access Point 1: `192.168.1.40`
        *   NanoStation TX: `192.168.1.45`
        *   NanoStation RX: `192.168.1.46`
        *   (e così via, mantenendo un schema logico)
*   **Dispositivi Mobili (Telefoni Camerieri, Tablet):**
    *   Questi possono tranquillamente ottenere un indirizzo IP dinamico dal server DHCP del router (nel range `192.168.1.50` - `192.168.1.200`).
    *   Dovranno essere configurati (o l'app SagraFacile dovrà essere configurata) per puntare all'IP statico del server SagraFacile (es. `https://192.168.1.10`).

**Importante:** Documenta tutti gli IP statici assegnati per futura referenza.

**Lista della Spesa (Indicativa e Budget-Oriented):**

1.  **Cavo Ethernet:**
    *   Matassa 100m Cat 6 U/FTP rame solido: ~50-80€ (ne userai ~60m, ma avere margine è utile)
    *   Connettori RJ45 Cat 6, Frutti Keystone Cat 6, Patch Panel (opzionale, per terminazioni pulite): ~20-40€
2.  **Switch Gigabit Unmanaged:**
    *   2-3 x Switch 5/8 porte Gigabit (TP-Link LS1005G/LS1008G, Netgear GS305/GS308): ~15-25€ cadauno. Totale: ~30-75€
3.  **Access Point Wi-Fi (per Capannoni):**
    *   *Opzione Pro Budget:* 2 x Ubiquiti UniFi AP AC Lite o TP-Link EAP225: ~80-100€ cadauno. Totale: ~160-200€ (includono iniettori PoE)
    *   *Opzione Super Budget:* 2 x Router Wi-Fi consumer (TP-Link Archer C6/A6): ~30-40€ cadauno. Totale: ~60-80€
4.  **Ponte Radio (per Paninoteca):**
    *   2 x Ubiquiti NanoStation Loco M5: ~50-60€ cadauna. Totale: ~100-120€
    *   (Oppure 2 x NanoStation 5AC Loco: ~60-70€ cadauna. Totale: ~120-140€)
    *   Staffe di montaggio se necessarie: ~10-20€
5.  **Cavi Patch Ethernet Corti:**
    *   Vari cavi Cat 5e/6 (0.5m, 1m, 2m) per collegare PC, switch, AP: ~20-30€ in totale.

**Stima Totale Budget (molto approssimativa):**

*   **Soluzione con AP Pro Budget:** ~300 - 450€
*   **Soluzione con AP Super Budget:** ~230 - 350€

**Consigli per l'Elettricista (per il cavo da 60m):**

*   "Posare un cavo Cat 6 U/FTP o F/UTP, conduttori in rame solido, dal punto X (Cassa) al punto Y (Capannoni)."
*   "Terminarlo su entrambe le estremità con prese Keystone Cat 6 o direttamente con connettori RJ45 maschio Cat 6 (se va diretto a uno switch)."
*   "Se possibile, farlo passare in un corrugato separato dai cavi elettrici o mantenere la massima distanza."
*   "Evitare curve troppo strette."
*   "Testare la continuità e la mappatura dei fili dopo la posa."

**Considerazioni Finali:**

*   **Linea d'Aria per NanoStation:** È FONDAMENTALE. Assicurati che ci sia visibilità diretta tra i due punti dove monterai le NanoStation. Alberi, muri, o altre strutture possono bloccare o degradare il segnale.
*   **Alimentazione PoE:** Le NanoStation e gli AP UniFi/Omada sono PoE. Significa che l'alimentazione arriva tramite il cavo di rete. Vengono forniti con i loro "iniettori PoE" (piccoli trasformatori con ingresso dati, uscita dati+power). Lo switch S2 (Capannoni) potrebbe essere uno switch PoE, semplificando i cablaggi per gli AP, ma costa di più.
*   **Backup:** Per una sagra, avere uno switch economico di scorta o un AP di scorta può salvare la serata in caso di guasti.
*   **Configurazione Iniziale:** Prenditi del tempo prima della sagra per configurare tutto in un ambiente controllato, testare i collegamenti e le velocità.
*   **Sicurezza:** Cambia le password di default di tutti i dispositivi (router, AP, NanoStation).

Questo piano dovrebbe darti una base solida. Fammi sapere se hai altre domande o se vuoi approfondire qualche aspetto!
