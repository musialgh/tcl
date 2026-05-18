// ===============================================
// Implementation of cross-majority-skating scheme 
// ===============================================


import "tas-evaluation-interface";


evaluate_round_elimination(round):
evaluate_round_final(round, do_skating = FALSE):
perform_skating(unplaced, tie, starts, jury, round, dances, final_marks, for_place):


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


/// Evaluate one dance in a final
evaluate_dance_final:
    /// *all* starts participating in the dance, key = value
    field     : { start_no+ : nat },
    /// marks of *remaining* (unplaced) starts only
    marks(template_evaluate_round), ///< use same-name argument pattern from template
    dance     : txt,
    for_place : nat = 1,
->
    report(template_evaluate_round)

evaluate_dance_final(field, marks, dance, for_place) =
    // terminate when all starts have been placed
    report_up                                                                       if #unplaced = 0 |
    // continue with next free place for all remaining starts
    merge(report_up, eval_dance(field, unplaced, dance, for_place + #marks - #unplaced))
where
    // invoke "tie" refinement for the current place, which will return a report
    report_up = refine_count_sum(field, marks, dance, for_place),
    // determine starts still unplaced, by removing start(s) just placed by above call
    unplaced = filter(start, _ in marks, not ?report_up[start][dance][""][""][""]["place_min"]);
// evaluate_dance_final()


/// Recursively refine a tie for <for_place>, starting with R5 from <by_place>, for a single dance *or* R11
refine_count_sum:
    field(evaluate_dance_final),
    marks(template_evaluate_round),
    dance       : txt = "", ///< empty string as default key for R11 application
    for_place   : nat,
    by_place    : nat = 1
->
    report(template_evaluate_round);

refine_count_sum(field, marks, dance, for_place, by_place) =
    merge(report_counts,              r5_single)                            if #r5_choice = 1    |
    merge(report_counts, report_sums, r7_single)                            if #r7_choice = 1    |
    merge(report_counts, report_sums, report_up)                            if by_place < #field |
    merge(report_counts, report_sums, r7_shared)                            if #r7_choice > 1    |
    abort("internal error: unable to resolve tie of {#marks} starts for place {for_place}, dance <{dance}>"
          "\nreport on current place: {merge(report_counts, report_sums)}"
    )
where
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
    report_up = refine_count_sum(field, r7_unplaced, dance, for_place, by_place + 1);
// refine_count_sum()    








/// Evaluate a full final round with majority rules, no skating yet
evaluate_round_final(template_evaluate_round): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// place ranges use natural numbers, place value may be fractional (average)
    places_out  : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } },
    /// report sum of place values
    report_out  : { start_no+ : { "places-sum" : real } };  

evaluate_round_final(marks, places) = 
    report_out = map(
        my_no in field, 
        { "places-sum" : sum(dance in places, dance[my_no]["pval"]) }
    ),
    // perform ordering by sum (lower is better)
    places_out = compute_places(report_out)
where
    field = keys(first(places));



/// Evaluate a full final round with skating
evaluate_round_final_skating(template_evaluate_round): 
    /// marks are natural numbers, jury is not partitioned
    marks   : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    /// place ranges use natural numbers, place value may be fractional (average)
    places  : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } }
->
    /// place ranges use natural numbers, place value may be fractional (average)
    places_out : { start_no+ : {  "pmin" : nat, "pmax" : nat, "pval" : real } },
    /// full report including skating stuff
    report_out :  { start_no+ : { "places-sum" : real, 
                                  place*       : { "R10" : nat, "R11"? : nat },
                                  "R10"?       : { place+ : { "count" : nat, "sum" : real } },
                                  "R11"?       : { place+ : { "count" : nat, "sum" : nat  } }
                  } };

evaluate_round_final_skating(marks, places) = 
    report_out = report_up,
    places_out = compute_places(scores_up) // final scoring delivers final placing
where
    // number of places in list: all except last possible
    num = count(first(places));
    // invoke majority system as a basis
    majority_places, majority_report = evaluate_round_final(marks, places),
    // initialize scores with single place value obtained from majority evaluation
    scores_down = map(start in majority_places, { 0 : start["pval"] });
    // invoke recursive refinement from here
    report_up, scores_up = skating_refine_r10(marks, places, majority_report, scores_down);



/// Refine assignment of places starting with <for place> according to skating rules, whenever neccessary.
/// The implementation of the function body will just apply R10 for <for_place>,
/// but then recursively invoke either R10 on the next place, or R11 on the same.
skating_refine_r10:
    marks       : { dance+ : { start_no+ : { "jury" : { adjudicator_short+ : nat } } } }, 
    places      : { dance+ : { start_no+ : { "pmin" : nat, "pmax" : nat, "pval" : real } } },
    /// structured table of report values per start (see return for format)
    report      : { start_no+ : object },
    /// score list as far as already constructed  (see return for format)
    scores      : { start_no+ : object },
    /// target place to be resolved next (default value is for start of recursion)
    for_place   : nat = 1
->
    report_out  : { start_no+ : { "places-sum" : real, 
                                  place*       : { "R10" : nat, "R11"? : nat },
                                  "R10"?       : { place+ : { "count" : nat, "sum" : real } },
                                  "R11"?       : { place+ : { "count" : nat, "sum" : nat  } }
                  } },
    scores_out  : { start_no+ : { 0      : real,                            // places-sum
                                  place* : { "R10" : nat, "R11"? : nat }    // places assigned by skating
                  } };

skating_refine_r10(marks, places, report, scores, for_place) =
    { "report_out" : report, "scores_out" : scores }                            if for_place > #field |
    skating_refine_r10(marks, places,     report,     scores, for_place + 1)    if #candidates < 2    |
    skating_refine_r11(marks, places, r10_report, r10_scores, for_place    )
where 
    field           = keys(first(places)),
    r10_places      = compute_places(scores),
    candidates      = keys(filter(range in r10_places, range["pmin"] = for_place)),
    // please note that everything below is only computed if <r10_scores> is required above for recursion
    // compute counts and sums for same-or-better placings over whole dances (*no* majority clipping!)
    r10_data        = map(
        start in candidates,
        { "rule10" : { for_place : count_and_sum(for_place, map(dance in places, dance[start]["pval"])) } }    
    ),
    // compute R10 score values (negating counts, as higher count is better)
    r10_values      = map(
        start in r10_data, 
        { 1 : -start["rule10"][for_place]["count"], 2 : +start["rule10"][for_place]["sum"] }
    ),
    // determine best value of competing starts
    r10_chosen      = arg_min(r10_values), // returns key -> key mapping(s) for all occurrences of the minimum
    // create a new R10 coulumn using resulting places for all candidates:
    // - resulting place is <for_place> for each occurrence of the optimum value
    // - resulting place is next one assignable for all others (discrimination within this group later via R11)
    r10_placing     = map(
        start in candidates, 
        { for_place : { "R10" : for_place               } } if ?r10_chosen[start] | 
        { for_place : { "R10" : for_place + #r10_chosen } }
    ),
    // recursively merge new column with existing score lists
    // -- then, all starts competing for <for_place> have been refined by way of the new column,
    //    while all others are correctly discriminated via the very first column (place-sum) anyway
    r10_scores      = merge(scores, r10_placing),
    r10_report      = merge(report, r10_placing, r10_data);

        
/// Refine assignment of places starting with <for place> according to skating rules, whenever neccessary.
/// The implementation of the function body will just apply R11 for <for_place>,
/// but then recursively invoke R10 on the next place
skating_refine_r11(skating_refine_r10); // using r10 signature unchanged => no need to re-define

skating_refine_r11(marks, places, report, scores, for_place) =
    { "report_out" : report, "scores_out" : scores }                            if for_place > #field |
    skating_refine_r10(marks, places,     report,     scores, for_place + 1)    if #candidates < 2    |
    skating_refine_r10(marks, places, r11_report, r11_scores, for_place + 1)
where 
    field           = keys(first(places)),
    r11_places      = compute_places(scores),
    candidates      = keys(filter(range in r11_places, range["pmin"] = for_place)),
    // please note that everything below is only computed if <r11_scores> is required above for recursion
    // flatten marks for all dances, per start, under a "merged key" for adjudicators and dances
    r11_marks       = merge(
        dkey, dance in marks,
        map(
            my_marks in dance,
            merge(
                jkey, my_mark in my_marks["jury"],
                { { 1 : jkey, 2 : dkey } : my_mark } // objects are sortable and may therefore serve as keys
            )
        )    
    ),
    // compute counts and sums for candidate starts as for single dances, but only from <for_dance> onwards
    r11_data        = map(
        start in candidates,
        { 
            "rule11" : map(
                place in for_place..#field, // map() works since range sets key = value pairs
                count_and_sum_maj(place, r11_marks[start])
            )
        }
    ),
    // negate counts (higher is better) and use safely-ordered keys -- not relying on "count" < "sum" ;-)
    r11_values      = map(start in r11_data, map(place in start["rule11"], { 1 : -place["count"], 2 : +place["sum"] })),
    // determine best value of competing starts
    r11_chosen      = arg_min(r11_values), // returns key -> key mapping(s) for all occurrences of the minimum
    // create a new r11 coulumn using resulting places for all candidates:
    // - resulting place is <for_place> for each occurrence of the optimum value
    // - resulting place is next one assignable for all others (discrimination within this group later via R11)
    r11_placing     = map(
        start in candidates, 
        { for_place : { "R11" : for_place               } } if ?r11_chosen[start] | 
        { for_place : { "R11" : for_place + #r11_chosen } }
    ),
    // recursively merge new column with existing score lists
    // -- then, all starts competing for <for_place> have been refined by way of the new column,
    //    while all others are correctly discriminated via the very first column (place-sum) anyway
    r11_scores      = merge(scores, r11_placing),
    // as <r11_data> may overlap with earlier calls, we need to use replace() instead of merge()
    r11_report      = replace(merge(report, r11_placing), r11_data);



// Helpers =============================================================================================================


/// Process a single or shared place assigment for a dance *or* round 
assign_place:
    start_no    : nat,
    dance       : txt = "", ///< empty string as default key for non-dance-specific entries
    place_min   : nat,
    place_max   : nat,
    reason      : txt
->
    report(template_evaluate_round);

assign_place(start_no, dance, place_min, place_max, reason) =
    { start_no : { dance : { "" : { "" : { 
        "place_min" : { "value" : place_min, "reason" : reason }, 
        "place_max" : { "value" : place_max, "reason" : reason } 
    } } } } };






/// Calculate count and sum of "x and better" places
count_and_sum: 
    place : nat, marks : { origin+ : real } -> count : nat, sum : nat;

count_and_sum(place, marks) =
    count =  #relevant,   
    sum   =  sum(relevant)
where
    relevant = filter(mark in marks, mark <= place);



/// Calculate count and sum of "x and better" places, (0,0) if majority is not reached
count_and_sum_maj(count_and_sum); // re-use signature

count_and_sum_maj(place, marks) =
    data if data["count"] > (#marks / 2) | { "count" : 0, "sum" : 0 }
where
    data = count_and_sum(place, marks);
