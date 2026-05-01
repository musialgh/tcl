// Implementations specific to cross-majority-skating scheme ===========================================================


/// Evaluate a single dance in an elimination round
evaluate_dance_elimination(template_evaluate_part): 
    /// marks by adjudicator "initials", optionally structured in groups, for each participant, then for each part of the round (e.g. dance)
    marks   : { start_no+ : { "jury" : { adjudicator_short+ : num } } }
->
    /// optional data for result reports, optionally structured in groups
    report  : { start_no+ : { "count" : nat } },            
    /// place range pmin..pmax (sorting on pmin used for effective ranking; fractional placing like pmin=pmax=3.5 permitted)
    places  : { start_no+ : {  "pmin" : num, "pmax" : num } };

evaluate_dance_elimination(marks) = 
    report, places
where
    places = compute_places(score),
    report = map(my_marks in marks, score_dance_elimination(my_marks["jury"])["report"])
where    
    score  = map(my_marks in marks, score_dance_elimination(my_marks["jury"])["score" ]);


/// Evaluate a single dance in a final round, using majority rules, no skaing yet
evaluate_dance_final(template_evaluate_part): 
    /// marks by adjudicator "initials", optionally structured in groups, for each participant, then for each part of the round (e.g. dance)
    marks   : { start_no+ : { "jury" : { adjudicator_short+ : num } } } 
->
    /// optional data for result reports, optionally structured in groups
    report  : { start_no+ : { place+ : { "count" : nat, "sum" : nat } } },            
    /// place range pmin..pmax (sorting on pmin used for effective ranking; fractional placing like pmin=pmax=3.5 permitted)
    places  : { start_no+ : {  "pmin" : num, "pmax" : num } };

evaluate_dance_final(marks) = 
    report, places
where
    places = compute_places(score),
    report = map(my_marks in marks, score_dance_final(my_marks["jury"])["report"])
where    
    score  = map(my_marks in marks, score_dance_final(my_marks["jury"])["score" ]);


/// Convert a sequence of scores into a sequence of place ranges.
/// In this case, place ranges are aggregated as an average place (as required for single-dance placings)
compute_places: 
    scores : { start_no+ : object } 
->
    places : { start_no+ : { "pmin" : real, "pmax" : real } };

compute_places(scores) =
    map(score in scores, { "pmin": pmin, "pmax": pmax })
where 
    pmin = 1       + count(other_score in scores, other_score > score) + 1,
    pmax = #scores - count(other_score in scores, other_score < score);


/// Compute comparable score for elimination dance with cross markings (expressed as bool, i.e. 0/1)
score_dance_elimination:
    marks   : { adjudicator_short+ : bool },
->  
    report  : { "count" : nat },  
    score   : { "count" : nat }; ///< sequence of numbers, higher is better, for priorized comparison

score_dance_elimination(marks) =
    report: count(marks), // using values from range if no value expression given
    score:  report;


/// Compute comparable score for final dance with majority rule (no skating here)
score_dance_final(template_evaluate_part):
    marks       : { adjudicator_short+ : nat },
    num_starts  : nat
->
    report      : { place+ : { "count" : nat, "sum" : nat } },  
    score       : { index+ : nat }; ///< sequence of numbers, higher is better, for priorized comparison

evaluate_dance_final(marks, num_starts) = 
    report, score // for labelled outputs, this is short for: "report" : report, "score" : score
where
    report = object(place in 1..num_starts-1, key: place, value: count_and_sum(place, marks)),
    score  = concat(place in 1..num_starts-1, { 2*place-1 : +report[place]["count"], 2*place : -report[place]["sum"] });


/// Calculate count and sum of "x and better" places, clipped to (0,0) if majority is not reached
count_and_sum: nat, { adjudicator_short+ : nat } -> count : nat, sum : nat;

count_and_sum(place, marks) =
    count:     #relevant    if is_majority | 0,
    sum:    sum(relevant)   if is_majority | 0
where
    relevant    = filter(mark in marks, mark <= place),
    is_majority = #relevant > (#marks / 2);








/// Evaluate a full final round with majority rules, no skating yet
evaluate_round_final(template_evaluate_round): 
    /// marks by adjudicator "initials", optionally structured in groups, for each participant, then for each part of the round (e.g. dance)
    marks   : { dance+ : { start_no+ : { adjudicators_group+ : { adjudicator_short+ : num } } } }, 
    /// placings from the individual round parts
    places  : { dance+ : { start_no+ : { "pmin" : num, "pmax" : num } } },
->
    /// optional data for result reports, optionally structured in groups
    report  : { start_no+ : { colum-tree+ : any } },            
    /// place range pmin..pmax (sorting on pmin used for effective ranking; fractional placing like pmin=pmax=3.5 permitted)
    places  : { start_no+ : {  "pmin" : num, "pmax" : num } };

evaluate_round_final(marks, places) = 
    report, place
where
    place = compute_places(score),
    report = map(my_marks in marks, score_dance_final(my_marks["jury"], params["num_starts"])["report"])
where    
    score  = map(my_marks in marks, score_dance_final(my_marks["jury"], params["num_starts"])["score" ]);



