// Wrappers for dance evaluation followed by round evaluation, written in TCL



wrap_round_final_skating:
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : bool } } } }
->
    report  : { start_no+ : object },            
    places  : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } };


wrap_round_final_skating(marks) =
    report = merge(round_report, dance_reports),
    places = round_places
where
    field = keys(first(marks)),
    // invoke single-dance evaluation for each dance
    results = map(
        dance_marks in marks,
        evaluate_dance_final(dance_marks)
    ),
    // concatenate reports for round evaluation, with start_no as the top-level key: 
    // start_no+ : { dance+ : { group* : ... { field+ : num } } }
    dance_reports = map(
        start in field, 
        map(
            dance_result in results,
            dance_result["report"][start]
        )
    ),
    // collect placings for round evaluation, with dance key inside:
    // dance+ : { start_no+ : { place-info+ : num } }
    dance_places = map(
        dance_result in results,
        dance_result["places"]
    ),
    // hand over to round evaluation, which will use the dance placings as input
    round_places, round_report = evaluate_round_final_skating(marks, dance_places);
