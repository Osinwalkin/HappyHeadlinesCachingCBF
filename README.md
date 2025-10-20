# Caching og Monitoring til HappyHeadlines

Jeg glemte at lave et github repository da jeg lavede opgaven... så der er desværre ikke nogen git historik.

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