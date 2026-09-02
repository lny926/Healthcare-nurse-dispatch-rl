def calculate_care_quality_score_from_values(
    completion_rate,
    average_waiting_time_min,
    p95_waiting_time_min,
    escalations,
    average_fatigue,
    workload_std,
    distance_std
):
    score = (
        completion_rate * 100
        - average_waiting_time_min * 2
        - p95_waiting_time_min * 1
        - escalations * 0.5
        - average_fatigue * 20
        - workload_std * 2
        - distance_std * 0.02
    )

    return score


def calculate_care_quality_score_from_results(results):
    average_waiting_time_min = results["average_waiting_time"] / 60
    p95_waiting_time_min = results["p95_waiting_time"] / 60

    return calculate_care_quality_score_from_values(
        completion_rate=results["completion_rate"],
        average_waiting_time_min=average_waiting_time_min,
        p95_waiting_time_min=p95_waiting_time_min,
        escalations=results["escalations"],
        average_fatigue=results["average_fatigue"],
        workload_std=results["workload_std"],
        distance_std=results["distance_std"]
    )


def calculate_care_quality_score_from_summary(summary):
    return calculate_care_quality_score_from_values(
        completion_rate=summary["completion_rate_mean"],
        average_waiting_time_min=summary["average_waiting_time_min_mean"],
        p95_waiting_time_min=summary["p95_waiting_time_min_mean"],
        escalations=summary["escalations_mean"],
        average_fatigue=summary["average_fatigue_mean"],
        workload_std=summary["workload_std_mean"],
        distance_std=summary["distance_std_mean"]
    )