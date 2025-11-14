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

### Demonstration til AKF Scale Cube Q2

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

#### **Sådan testes scaling**

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

---

### Demonstration til Fault Isolation (Circuit Breaker) Q3

Denne demo viser, hvordan jeg i `CommentService` implementerer fault isolation ved hjælp af et Circuit Breaker-mønster for at beskytte sig mod fejl i en afhængig service (en simuleret `ProfanityService`).

#### **Arkitekturændringer**

*   **CommentService**: Er blevet opdateret med `Polly`-biblioteket for at implementere et Circuit Breaker.
*   **Circuit Breaker Logik**: Kredsløbet er konfigureret til at "trippe" (åbne) efter 3 efterfølgende fejl, når der kaldes til `ProfanityService`.
*   **Fallback Mekanisme**: Når kredsløbet er åbent, forsøger `CommentService` ikke længere at kalde den fejlende service. I stedet aktiveres en fallback-logik med det samme: kommentaren accepteres og markeres internt til senere moderation. Dette sikrer, at systemet forbliver funktionelt for brugeren, selvom en underliggende service er nede.
*   **Nyt Endpoint**: Et nyt endpoint `POST /comments` er blevet tilføjet for at kunne demonstrere denne funktionalitet.

#### **Sådan kan man køre det**

1.  Man skal self bare have docker og docker compose installeret, og så køre man det sådan.

    ```bash
    docker-compose up --build
    ```

#### **Sådan tester man det**

Denne demonstration observeres bedst ved at følge log-outputtet fra `commentservice-1` containeren i din Docker-terminal, mens du sender anmodninger fra en anden terminal.

1.  Åbn en ny terminal.
2.  Send den følgende `curl`-anmodning **mindst 4 gange i træk**.

    ```bash
    curl -X POST -H "Content-Type: application/json" -d '{"author":"Demo Bruger","text":"Dette er en test"}' http://localhost:8002/comments
    ```
3.  Observer logfilerne fra `commentservice-1` i din Docker-terminal. Du vil se følgende sekvens:

    *   **Anmodning 1, 2 og 3:** Loggen vil vise en fejl, der bliver fanget. Dette er kredsløbet, der tæller fejlene.
        ```
        --> ProfanityService is unreachable. Circuit breaker is counting this failure.
        ```

    *   **Anmodning 4 (og efterfølgende):** Loggen vil nu vise, at kredsløbet er åbent, og at fallback-logikken bliver brugt. **Der bliver ikke længere forsøgt et netværkskald.**
        ```
        --> Circuit is open. ProfanityService is down. Using FALLBACK.
        ```
        Denne sekvens beviser, at Circuit Breaker-mønstret virker: det isolerer fejlen og lader systemet fortsætte med at fungere på en fornuftig måde.

---

### Demonstration til logging og tracing Q4

Denne demonstration vil prøve at vise principperne fra uge 38 og 39: struktureret logging og distribueret tracing. Den demonstrerer, hvordan et `TraceId` kan propagere fra en (simuleret) upstream service til `ArticleService`.

#### **Arkitekturændringer**

*   **ArticleService**: Er blevet opdateret med **Serilog** for at producere struktureret JSON-logging. Dette gør logs maskinlæsbare og lettere at analysere i et centralt log-system.
*   **Trace Context Propagering**: `/articles/{id}` endpointet er blevet modificeret, så det nu inspicerer indkommende HTTP-kald for en header ved navn `Trace-Id`.
*   **Log Enrichment**: Hvis en `Trace-Id` header findes, bliver dens værdi automatisk tilføjet til alle logs, der genereres under behandlingen af det pågældende kald. Hvis headeren mangler, genereres et nyt, unikt `TraceId`.

#### **Sådan kan man køre det**

1.  Sørg for at docker og docker compose er installeret.

    ```bash
    docker-compose up --build
    ```

#### **Sådan tester man det**

Verifikationen sker ved at observere log-outputtet fra `articleservice` containerne i Docker, mens du sender to forskellige anmodninger.

1.  **Test 1: Generering af en ny Trace ID**
    Send en standard `curl`-anmodning uden en header.

    ```bash
    curl http://localhost:8001/articles/2
    ```    
    I Docker-loggen vil du se en struktureret JSON-logbesked. Læg mærke til `TraceId`-feltet, som nu indeholder et auto-genereret GUID.

    **Eksempel på output i loggen:**
    ```json
    {
        "Timestamp": "...",
        "Level": "Information",
        "MessageTemplate": "Cache HIT for article {ArticleId}",
        "Properties": {
            "ArticleId": 2,
            "TraceId": "6f762b1a-230e-48d6-ab6b-e44f97ca7ae3"
        }
    }
    ```

2.  **Test 2: Propagering af en eksisterende Trace ID**
    Send en ny `curl`-anmodning, men tilføj denne gang `Trace-Id` headeren for at simulere et kald fra en upstream service som `PublisherService`.

    ```bash
    curl -H "Trace-Id: fra-publisher-service-abcde" http://localhost:8001/articles/2
    ```
    I Docker-loggen vil du se en ny logbesked. Læg mærke til, at `TraceId`-feltet nu indeholder præcis den værdi, du sendte med i headeren.

    **Eksempel på output i loggen:**
    ```json
    {
        "Timestamp": "...",
        "Level": "Information",
        "MessageTemplate": "Cache HIT for article {ArticleId}",
        "Properties": {
            "ArticleId": 2,
            "TraceId": "fra-publisher-service-abcde"
        }
    }
    ```    
    Dette bekræfter, at trace-konteksten er blevet succesfuldt propageret på tværs af servicegrænsen.