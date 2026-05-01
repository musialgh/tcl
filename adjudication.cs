// Signature templates (to be matched by all adjucation schemes) =======================================================


/// Evaluate a part of a round (e.g. a single dance)
template_evaluate_part: 
    /// marks by adjudicator "initials", optionally structured in groups, for each participant, then for each part of the round (e.g. dance)
    marks   : { start_no+ : { adjudicators_group+ : { adjudicator_short+ : num } } }, 
    /// optional parameters needed for evaluation
    params? : { param+ : num }
->
    /// optional data for result reports, optionally structured in groups
    report? : { start_no+ : { colum-tree+ : any } },            
    /// place range pmin..pmax (sorting on pmin used for effective ranking; fractional placing like pmin=pmax=3.5 permitted)
    places  : { start_no+ : { "pmin" : num, "pmax" : num } };


/// Evaluate a full round, potentially consisting of multiple parts (e.g. dances)
template_evaluate_round: 
    /// marks by adjudicator "initials", optionally structured in groups, for each participant, then for each part of the round (e.g. dance)
    marks   : { part+ : { start_no+ : { adjudicators_group+ : { adjudicator_short+ : num } } } }, 
    /// placings from the individual round parts
    places  : { part+ : { start_no+ : { "pmin" : num, "pmax" : num } } },
    /// optional parameters needed for evaluation
    params? : { param+ : num }
->
    /// optional data for result reports, optionally structured in groups
    report? : { start_no+ : { colum-tree+ : any } },            
    /// place range pmin..pmax (sorting on pmin used for effective ranking; fractional placing like pmin=pmax=3.5 permitted)
    places  : { start_no+ : {  "pmin" : num, "pmax" : num } };


// Import all adjudication libraries ===================================================================================

import cross-majority-skating as cms;
// import equality_classification as eq_cls;
// ...
