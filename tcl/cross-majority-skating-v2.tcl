// ===============================================
// Implementation of cross-majority-skating scheme 
// ===============================================


import "tas-evaluation-interface";


/// Evaluate elimination round (crosses)
evaluate_round_elimination(template_evaluate_round); // use template signature unchanged

evaluate_round_elimination(marks) = 
    report = merge(dance_counts, total_counts, placings)
where
    // compute number of crosses per dance
    dance_counts = map(
        my_marks in marks, map(
            dance in my_marks, 
            { "" : { "" : { "" : { "marks" : { 
                "value"  : sum(dance), // recursive sum of values
                "reason" : "F6.8.1: count of marks"
            } } } } } 
        )
    ),
    // compute total number of crosses
    total_counts = map(
        my_marks in marks,
        { "" : { "" : { "" : { "" : { "marks" : { 
            "value"  : sum(my_marks), // recursive sum of values
            "reason" : "F6.8.1: count of marks"
        } } } } } }
    ),
    // generate placings
    placings = map(
        my_marks in total_counts, 
        { "" : { "" : { "" : { "" : { 
            "place_min" : { "value"  : pmin, "reason" : reason },
            "place_max" : { "value"  : pmax, "reason" : reason }
        } } } } }
    where
        // re-read accumulated data from report -- not beautiful, but prevents redundant calculation
        my_value = my_marks[""][""][""][""]["marks"]["value"],
        pmin = 1             + count(peer in total_counts, peer[""][""][""][""]["marks"]["value"] > my_value),
        pmax = #total_counts - count(peer in total_counts, peer[""][""][""][""]["marks"]["value"] < my_value),    
        reason = "F6.8.1: single rank {pmin} in count of marks"           if pmin = pmax
               | "F6.8.1: shared rank {pmin}..{pmax} in count of marks"
    );
// evaluate_round_elimination()


/// Evaluate one dance in a final, for not-yet-placed <marks>
evaluate_dance_final:
    /// *all* starts participating in the round, key = value
    field     : { start_no+ : nat },
    /// marks of *remaining* (unplaced) starts only
    marks(template_evaluate_round), ///< use same-name argument pattern from template
    dance     : txt
->
    report(template_evaluate_round)

evaluate_dance_final(field, marks, dance) =
    // terminate when all starts have been placed
    report_up                                                   if #unplaced = 0 |
    // continue with next free place for all remaining starts
    // need to combine report parts with replace(), as merge() would fail on duplicate count/sum reports
    replace(report_up, evaluate_dance_final(field, unplaced, dance))
where
    // invoke "tie" refinement for the current place on counts and sums, which will return a report
    report_up = refine_count_sum(field, marks, dance),
    // determine starts still unplaced, by removing start(s) just placed by above call
    unplaced = filter(start, _ in marks, not ?report_up[start][dance][""][""][""]["place_min"]);
// evaluate_dance_final()


/// Recursively refine a tie on not-yet-placed <marks>, starting with R5 from <by_place>, for a single dance *or* R11
refine_count_sum:
    field(evaluate_dance_final),
    marks(evaluate_dance_final),
    dance       : txt = "", ///< empty string as default key for R11 application
    by_place    : nat = 1
->
    report(template_evaluate_round);

refine_count_sum(field, marks, dance, by_place) =
    merge(report_counts,              r5_single)                            if #r5_choice = 1    |
    merge(report_counts, report_sums, r7_single)                            if #r7_choice = 1    |
    merge(report_counts, report_sums, report_up)                            if by_place < #field |
    merge(report_counts, report_sums, r7_shared)                            if #r7_choice > 1    |
    abort("internal error: unable to resolve tie of {#marks} starts for place {for_place}, dance <{dance}>"
          "\nreport on current place: {merge(report_counts, report_sums)}"
    )
where
    for_place = #field - #marks + 1,
    jury = keys(first(marks)[dance][""]),
    // extract relevant count column, no matter if majority or not
    r5_counts_all = map(my_marks in marks, count(mark in my_marks[dance][""], mark <= by_place)),
    // suppress sub-majority results (note: TSO also reports sub-majority counts -- but we don't like that!)
    r5_counts = filter(count in r5_counts_all, 2 * count > #jury),
    
    // report counts considered
    report_counts = map(
        my_count in r5_counts,
        { dance : { by_place : { "" : { "" : { "count": { 
            "value"  : my_count, 
            "reason" : "R5,6: count of marks for places {by_place} and better"
        } } } } } }
    ),    
    // filter marks by maximum count (possibly empty list!)
    r5_choice   = arg_max(r5_counts), // returns key -> key mapping(s) for all occurrences of the maximum
    r5_unplaced = filter(start, _ in marks, not ?r5_choice[start]), // marks reduced to remaining starts 
    // report placing (controlled by lazy evaluation)
    r5_single   = assign_place(
        r5_choice, dance, for_place, for_place, 
        "R5,6,8: highest majority count {max(r5_counts)} on place {by_place}"
    ),

    // extract relevant sum column
    r7_sums = map(my_marks in r5_unplaced, sum(mark in my_marks[dance][""], mark if mark <= by_place | 0)),
    // report sums considered
    report_sums = map(
        my_sum in r7_sums,
        { dance : { by_place : { "" : { "" : { "sum": { 
            "value"  : my_sum, 
            "reason" : "R7a: sum of marks for places {by_place} and better"
        } } } } } }
    ),
    // filter marks by minimum sum (possibly empty list!)
    r7_choice   = arg_min(r7_sums), // returns key -> key mapping(s) for all occurrences of the minimum
    r7_unplaced = filter(start, _ in r5_unplaced, not ?r7_choice[start]), // marks reduced to remaining starts
    // report placing (controlled by lazy evaluation)
    r7_single     = assign_place(
        r7_choice, dance, for_place, for_place, 
        "R7a: highest majority count {max(r5_counts)} with lowest sum {min(r7_sums)} on place {by_place}"
    ),
    r7_shared   = assign_place(
        r7_choice, dance, for_place, for_place + #r7_choice - 1, 
        "R7c,d: shared highest majority count {max(r5_counts)}, sum {min(r7_sums)} on place {by_place}"
    ),

    // invoke evaluation of next place column, only if lazily evaluated by body expression
    report_up = refine_count_sum(field, r7_unplaced, dance, by_place + 1);
// refine_count_sum()    


/// Evaluate a full final round with majority rules, skating optional (default enabled)
evaluate_round_final: 
    marks(template_evaluate_round), ///< use same-name argument pattern from template
    enable_skating : bool = true
->
    report(template_evaluate_round);

evaluate_round_final(marks, enable_skating) = 
    merge(report_dances, report) // TODO
where
    field  = keys(marks),
    dances = keys(first(marks)),
    // evaluate all dances and combine reports
    report_dances = merge(dance in dances, evaluate_dance_final(field, marks, dance)),
    // extract place sums from report as start_no+ : real
    place_sums = map(
        start, my_report in report_dances, 
        sum(dance in dances, my_report[dance][""][""][""]["place_val"]["value"])
    ),
    // report place sums
    report_places = map(
        place_sum in place_sums,
        { "" : { "" : { "" : { "" : { "place_val" : { 
            "value"  : place_sum, 
            "reason" : "R9a: sum of place values from dances"
        } } } } } }
    ),
    // invoke resolution place by place
    report = refine_r9(marks, place_sums, enable_skating);
// evaluate_round_final()


/// Refine assignment of round placings starting with <for place> according to rules 9ff
refine_r9:
    field(evaluate_dance_final),
    marks(evaluate_dance_final),
    place_sums      : { start_no+ : real },
    enable_skating  : bool,
->
    report(template_evaluate_round);

refine_r9(field, marks, place_sums, enable_skating) =
    r9_single                                   if #r9_choice = 1     |
    r9_shared                                   if not enable_skating |
    merge(report_up, refine_r9(field, r10_unplaced, place_sums, enable_skating))
where
    for_place     = #field - #marks + 1,
    // only work on unplaced sums (not reduced on recursion)
    unplaced_sums = filter_keys(place_sums, marks), // filter arg1 according to keys in arg2
    // filter marks by minimum place sum
    r9_choice   = arg_min(unplaced_sums),
    r9_unplaced = filter(start, _ in marks, not ?r9_choice[start]), // marks reduced to remaining starts
    // report placing (controlled by lazy evaluation)
    r9_single   = assign_place(
        r9_choice, "", for_place, for_place,
        "R9a: lowest sum {min(unplaced_sums)} of places over all dances"
    ),
    r9_shared   = assign_place(
        r9_choice, "", for_place, for_place + #r9_choice - 1,
        "R9b: no skating => shared lowest sum {min(unplaced_sums)} of places over all dances"
    ),
    report_up    = refine_r10(field, r9_unplaced, for_place),
    r10_unplaced = filter(start, _ in r9_unplaced, not ?report_up[start][""][""][""]["place_min"]);
// refine_r9()




// Helpers =============================================================================================================


/// Process a single or shared place assigment for a dance *or* round 
assign_place:
    start_no    : { start_no+ : nat } = {}, ///< key = value mapping
    dance       : txt = "", ///< empty string as default key for R11 application
    place_min   : nat,
    place_max   : nat,
    reason      : txt
->
    report(template_evaluate_round);

assign_place(start_no, dance, place_min, place_max, reason) =
    { start_no : { dance : { "" : { "" : { "" : { merge(
        { "place_min" : { "value" : place_min,                   "reason" :     reason }}, 
        { "place_max" : { "value" : place_max,                   "reason" :     reason }},
        { "place-val" : { "value" : (place_min + place_max) / 2, "reason" : avg_reason }} if dance | {}
    ) } } } } } }
where
    // add hint for use within skating
    my_reason  = reason if dance | "R11 -> {reason}";
    // explain averaging only if applied -- not used in skating, since no place-val computed there
    avg_reason = "Rule 7d: average of {place-min}..{place_max}" if place_min != place_max | "";
// assign_place
