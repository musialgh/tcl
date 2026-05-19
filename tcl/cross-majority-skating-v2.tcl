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
    field     : { start_no+ : nat }, ///< all starts participating in the round, key = value
    candidates(template_evaluate_round::marks), ///< remaining unplaced starts (argument pattern from template)
    dance     : txt,
    for_place : nat = 1
->
    report(template_evaluate_round);

evaluate_dance_final(field, candidates, dance, for_place) =
    // terminate when all starts have been placed
    {}                                                                                if #candidates = 0 |
    // continue with next free place for all remaining starts
    // need to combine report parts with replace(), as merge() would fail on duplicate count/sum reports
    replace(report_up, evaluate_dance_final(field, unplaced, dance, for_place + #candidates - #unplaced))
where
    // invoke "tie" refinement for the current place on counts and sums, which will return a report
    report_up = refine_count_sum(field, candidates, dance, for_place),
    // determine starts still unplaced, by removing start(s) just placed by above call
    unplaced = filter(start, _ in candidates, not ?report_up[start][dance][""][""][""]["place_min"]);
// evaluate_dance_final()


/// Recursively refine a tie on unplaced <candidates>, starting with R5 from <by_place>, for a single dance *or* R11
refine_count_sum:
    field(evaluate_dance_final),
    candidates(evaluate_dance_final), ///< remaining tie
    dance       : txt = "", ///< empty string as default key for R11 application
    for_place   : nat,
    by_place    : nat = 1
->
    report(template_evaluate_round);

refine_count_sum(field, candidates, dance, for_place, by_place) =
    merge(report_counts,              r5_single)                            if #r5_choice = 1    |
    merge(report_counts, report_sums, r7_single)                            if #r7_choice = 1    |
    merge(report_counts, report_sums, report_up)                            if by_place < #field |
    merge(report_counts, report_sums, r7_shared)                            if #r7_choice > 1    |
    abort("internal error: unable to resolve tie of {#candidates} starts for place {for_place}, dance <{dance}>"
          "\nreport on current place: {merge(report_counts, report_sums)}"
    )
where
    // set helpers for single dance vs. R11 usage
    dances    = { dance : dance } if dance | keys(first(candidates)),
    marks_num = sum(d in dances, #first(candidates)[d][""]),
    // extract relevant count column, no matter if majority or not
    r5_counts_all = map(
        my_marks in candidates,
        sum(d in dances, count(mark in my_marks[d][""], mark <= by_place))
    ),
    // suppress sub-majority results (note: TSO also reports sub-majority counts -- but we don't like that!)
    r5_counts = filter(count in r5_counts_all, 2 * count > marks_num),
    
    // report counts considered
    report_counts = map(
        my_count in r5_counts,
        { dance : { by_place : { "" : { "" : { "count": { 
            "value"  : my_count, 
            "reason" : "R5,6: count of candidates for places {by_place} and better"
        } } } } } }
    ),    
    // filter candidates by maximum count (possibly empty list!)
    r5_choice     = arg_max(r5_counts), // returns key -> key mapping(s) for all occurrences of the maximum
    r5_candidates = filter_keys(candidates, r5_choice), // candidates reduced to choice
    // report placing (controlled by lazy evaluation)
    r5_single   = assign_place(
        r5_choice, dance, for_place, for_place, 
        "R5,6,8: highest majority count {max(r5_counts)} on place {by_place}"
    ),

    // extract relevant sum column
    r7_sums = map(
        my_marks in r5_candidates,
        sum(d in dances, sum(mark in my_marks[d][""], mark if mark <= by_place | 0))
    ),
    // report sums considered
    report_sums = map(
        my_sum in r7_sums,
        { dance : { by_place : { "" : { "" : { "sum": { 
            "value"  : my_sum, 
            "reason" : "R7a: sum of candidates for places {by_place} and better"
        } } } } } }
    ),
    // filter candidates by minimum sum (possibly empty list!)
    r7_choice     = arg_min(r7_sums), // returns key -> key mapping(s) for all occurrences of the minimum
    r7_candidates = filter_keys(r5_candidates, r7_choice), // candidates reduced to choice
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
    report_up = refine_count_sum(field, r7_candidates, dance, for_place, by_place + 1);
// refine_count_sum()    


/// Evaluate a full final round with majority rules, skating optional (default enabled)
evaluate_round_final: 
    marks(template_evaluate_round), ///< of remaining (unplaced) starts
    do_skating : bool = true
->
    report(template_evaluate_round);

evaluate_round_final(marks, do_skating) = 
    merge(report_dances, report_place_sums, r9_report)
where
    field  = keys(marks),
    dances = keys(first(marks)),
    // evaluate all dances and combine reports
    report_dances = merge(dance in dances, evaluate_dance_final(field, marks, 1, dance)),
    // extract placings per dance from report as: { start_no+ : { dance+ : real } }
    dance_placings = map(
        start, my_report in report_dances, 
        map(
             dance in dances,
             my_report[dance][""][""][""]["place_val"]["value"]
        )
    ),
    // extract place sums as: { start_no+ : real }
    place_sums = map(my_placings in dance_placings, sum(my_placings)),
    // report place sums
    report_place_sums = map(
        place_sum in place_sums,
        { "" : { "" : { "" : { "" : { "place_val" : { 
            "value"  : place_sum, 
            "reason" : "R9a: sum of place values from dances"
        } } } } } }
    ),
    // invoke resolution, starting with place 1
    r9_report = refine_r9(field, marks, dance_placings, place_sums, do_skating);
// evaluate_round_final()


/// Refine assignment of round placings according to rules 9ff
refine_r9:
    field(evaluate_dance_final),
    candidates(evaluate_dance_final), ///< remaining unplaced starts
    dance_placings  : { start_no+ : { dance+ : real } },
    place_sums      : { start_no+ : real },
    do_skating      : bool,
    for_place       : nat = 1,
->
    report(template_evaluate_round);

refine_r9(field, candidates, dance_placings, place_sums, do_skating, for_place) =
    {}  
        if #candidates = 0                   |
    // continue with remaing starts after a single placing according to R9
    merge(r9_single, refine_r9(field, r9_unplaced, dance_placings, place_sums, do_skating, r9_next))
        if #r9_choice  = 1                   |
    // continue with remaing starts after one or more placings from skating rules
    merge(sk_report, refine_r9(field, sk_unplaced, dance_placings, place_sums, do_skating, sk_next))
        if do_skating                        |
    // continue with remaing starts after a shared placing according to R9 (only if skating disabled)
    merge(r9_shared, refine_r9(field, r9_unplaced, dance_placings, place_sums, do_skating, r9_next))
where
    // only work on sums of current candidates
    our_sums      = filter_keys(place_sums, candidates), // filter arg1 according to keys in arg2
    // filter candidates by minimum place sum
    r9_choice     = arg_min(our_sums),
    r9_candidates = filter_keys(candidates, r9_choice), // candidates reduced to choice
    r9_unplaced   = remove_keys(candidates, r9_choice), // remaining candidates
    // report placing (controlled by lazy evaluation)
    r9_single     = assign_place(
        r9_choice, "", for_place, for_place,
        "R9a: lowest sum {min(our_sums)} of places over all dances"
    ),
    r9_shared     = assign_place(
        r9_choice, "", for_place, for_place + #r9_choice - 1,
        "R9b: no skating => shared lowest sum {min(our_sums)} of places over all dances"
    ),
    r9_next       = for_place + #r9_choice,
    // invoke skating (if select above, controlled by lazy evaluation)
    sk_report     = refine_skating(field, r9_candidates, dance_placings, for_place),
    // determine starts still unplaced, by removing start(s) just placed by above call
    sk_unplaced   = filter(start, _ in candidates, not ?sk_report[start][""][""][""]["place_min"]),
    sk_next       = for_place + #candidates - #sk_unplaced;
// refine_r9()


/// Refine assignment of round placings according to skating rules
refine_skating:
    field(evaluate_dance_final),
    candidates(refine_r9), ///< remaining tie
    dance_placings(refine_r9), ///< of all starts
    for_place : nat
->
    report(template_evaluate_round);

refine_skating(field, marks, dance_placings, for_place) =
    merge(r10_applied, report_counts,                           r10a_single) if #r10a_choice = 1 |
    merge(r10_applied, report_counts, report_sums,              r10c_single) if #r10c_choice = 1 |
    merge(r10_applied, report_counts, report_sums, r11_applied, report_r11 )
where
    // only work on placings of current candidates
    our_placings    = filter_keys(dance_placings, candidates),

    // report r10 application
    r10_applied = map(
        start in candidates,
        { "" : { "R10-applied" : { for_place : { "" : {  
            "value"  : "X",
            "reason" : "R9b: place sum tie on place {for_place}"
        } } } } }
    ),

    // obtain count of places equal to or better than target place
    r10_counts      = map(
        my_placings in our_placings,
        count(place in my_placings, place <= for_place)
    ),
    // report counts considered
    report_counts   = map(
        my_count in r10_counts,
        { "" : { "R10-criteria" : { for_place : { "" : { "count": { 
            "value"  : my_count, 
            "reason" : "R10a,b for place {for_place}"
        } } } } } }
    ),
    // filter candidates by maximum count
    r10a_choice     = arg_max(r10_counts),
    r10a_candidates = filter_keys(our_placings, r10a_choice), // candidates reduced to choice
    // report placing (controlled by lazy evaluation)
    r10a_single     = assign_place(
        r10a_choice, "", for_place, for_place + #r10a_choice - 1, 
        "R10a,b: highest count {max(r10_counts)} of places {for_place} and better over all dances"
    ),

    // obtain sums of places equal to or better than target place
    r10_sums        = map(
        my_placings in our_placings,
        sum(place in my_placings, place if place <= for_place | 0)
    ),
    // report sums considered
    report_sums     = map(
        my_sum in r10_sums,
        { "" : { "R10-criteria" : { for_place : { "" : { "sum": { 
            "value"  : my_sum, 
            "reason" : "R10c,d for place {for_place}"
        } } } } } }
    ),
    // filter candidates by minimum sum
    r10c_choice     = arg_min(r10_sums),
    r10c_candidates = filter_keys(our_placings, r10c_choice), // candidates reduced to choice
    // report placing (controlled by lazy evaluation)
    r10c_single     = assign_place(
        r10c_choice, "", for_place, for_place, 
        "R10c,d: lowest sum {min(r10_sums)} of places {for_place} and better over all dances"
    );

    // report r11 application
    r11_applied = map(
        start in r10c_candidates,
        { "" : { "R11-applied" : { for_place : { "" : {  
            "value"  : "X",
            "reason" : "R10e: count {max(r10_counts)}, sum {min(r10_sums)} "
                       "tie on place {for_place} or better over all dances"
        } } } } }
    ),
    // delegate to R11 (if required)
    report_r11 = refine_count_sum(field, r10c_candidates, dance : "", for_place, for_place);
// refine_skating()


// Helpers =============================================================================================================


/// Process a single or shared place assigment for a dance *or* round 
assign_place:
    start_no    : { start_no* : nat }, ///< key = value mapping
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
    avg_reason = "R7d: average of {place-min}..{place_max}" if place_min != place_max | "";
// assign_place
