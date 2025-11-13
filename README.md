# Caching og Monitoring til HappyHeadlines

Jeg glemte at lave et github repository da jeg lavede opgaven... så der er desværre ikke nogen git historik.

Jeg endte også med ikke at kunne få Grafana til at virke regelmæssigt... nogle gange virkede det og nogle gange ikke.

Dette projekt er en løsning til den første obligatoriske opgave i "Development of large systems". Formålet er at implementere to caching-lag for at forbedre performance og tilgængelighed for en applikation, samt at bygge et live monitoring-dashboard til at overvåge cache-effektiviteten.

Hele systemet er container-baseret og styres med Docker Compose.

## Systemarkitektur

Systemet består af følgende services:

*   **ArticleService**: Ansvarlig for artikler. Bruger et offline cache (Redis), der periodisk opdateres af en baggrundsservice.
*   **CommentService**: Ansvarlig for kommentarer. Bruger en cache-miss-strategi (Redis) med LRU (Least Recently Used) eviction policy.
*   **Redis**: In-memory datastore, der fungerer som cache for begge services.
*   **Prometheus**: Indsamler metrics (cache hits/misses) fra services via et `/metrics` endpoint.
*   **Grafana**: Viser de indsamlede metrics på et live dashboard.

## Forudsætninger

For at køre dette projekt skal du have følgende installeret:

*   Docker
*   Docker compose

## Kørsel af Projektet

1.  Klon dette repository.
2.  Naviger til root af projektmappen i din terminal.
3.  Kør følgende command for at bygge og starte alle services:

    ```
    docker-compose up --build
    ```

Systemet skulle nu gerne køre.


## Verificering og Test

Når containerne kører, kan du interagere med systemet via følgende endpoints:

*   **Prometheus UI**: http://localhost:9090
    *   (Her kan du tjekke Status -> Targets for at se, at articleservice og commentservice er `UP`).
*   **Grafana Dashboard**: http://localhost:3000
    *   **Login**: `admin` / `admin`
    *   Man skal selv oprette Prometheus som Data Source og bygge panelet.

### Generer Trafik

Åbn en ny terminal for at sende requests til dine services og se dashboardet opdatere i realtid.

**Eksempel på Cache Miss:**
```
# Anmodning til CommentService
curl http://localhost:8002/comments/article/555

# Anmodning til ArticleService
curl http://localhost:8001/articles/123
```

**Eksempel på Cache hit:**
```
# Eksempel på cache hit
curl http://localhost:8002/comments/article/555

# Cache hit, da den blev cachet i baggrunden
curl http://localhost:8001/articles/2
```

Hold øje med Grafana-dashboardet (husk at sætte tidsintervallet til "Last 5 minutes"), hvor du vil se din cache hit ratio ændre sig live.

---

## Obligatorisk Opgave 2

Denne sektion indeholder instruktioner til de demoer, der er forberedt til den anden obligatoriske opgave.

### Demonstration til AKF Scale Cube

Denne demo viser, hvordan `ArticleService` skaleres på X-aksen af AKF Scale Cube.

#### **Arkitekturændringer**

*   **ArticleService**: Er nu konfigureret til at køre som 3 identiske instanser. Servicens API-svar er blevet ændret til at inkludere containerens ID (`servedBy`), som bevis på, at anmodninger bliver håndteret af forskellige instanser.
*   **Nginx Load Balancer**: En ny `loadbalancer` service er tilføjet, som lytter på port `8001`. Den fungerer som det primære indgangspunkt (entry point) og fordeler trafik på tværs af de tre `ArticleService` instanser.

#### **Sådan kan man køre det**

1.  Sørg for at alle containere er stoppet (`docker-compose down`).
2.  Kør følgende kommando i projektets rodmappe. `--scale` flaget er essentielt, da det instruerer Docker Compose i at oprette 3 replikaer af `articleservice`.

    ```bash
    docker-compose up --build --scale articleservice=3
    ```

#### **Sådan verificeres scaling**

1.  Åbn et nyt terminalvindue ved siden af allerede kørende docker terminal.
2.  Send flere anmodninger til load balancerens endpoint på port `8001`.

    ```bash
    curl http://localhost:8001/articles/2
    ```
3.  Observer outputtet i din terminal. `servedBy`-feltet i JSON-svaret vil ændre værdi mellem anmodningerne. Dette bekræfter at load balancing virker.

    **Eksempel på output:**
    ```json
    // Anmodning 1
    {"article":{...},"servedBy":"ed6165072a5a"}

    // Anmodning 2
    {"article":{...},"servedBy":"3d7255f4c775"}
    ```
4.  Du kan også følge med i logfilerne fra `docker-compose` for at se "Cache HIT"-beskeder fra de forskellige service-instanser (f.eks. `articleservice-1`, `articleservice-2`).```