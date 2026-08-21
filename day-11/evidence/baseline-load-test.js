import http from "k6/http";
import { check } from "k6";

export const options = {
    scenarios: {
        baseline: {
            executor: "constant-vus",
            vus: 10,
            duration: "30s",
        },
    },
    thresholds: {
        http_req_failed: ["rate<0.01"],
        http_req_duration: ["p(99)<1000"],
    },
};

export default function () {
    const response = http.get(
        "http://localhost:5263/api/quotes/slow-authors"
    );

    check(response, {
        "status is 200": (r) => r.status === 200,
        "response contains 100 authors": (r) =>
            Array.isArray(r.json()) && r.json().length === 100,
    });
}