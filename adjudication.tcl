// Signature templates (to be matched by all adjucation schemes) =======================================================


/// Evaluate an element of a round (e.g. a single dance)
template_evaluate_element: 
    /// marks by adjudicator "initials", optionally structured in groups,
    /// for each participant, then for each element of the round (e.g. dance)
    marks   : { start_no+ : { adjudicators_group+ : { adjudicator_short+ : num } } }, 
    /// optional parameters needed for evaluation
    params? : { param+ : num }
->
    /// optional data for result reports, optionally structured in groups
    report? : { start_no+ : { colum-tree+ : any } },            
    /// place range pmin..pmax and representative value pval;
    /// results will be ordered according to the latter
    places  : { start_no+ : { "pmin" : num, "pmax" : num, "pval" : num } };


/// Evaluate a full round, potentially consisting of multiple elements (e.g. dances)
template_evaluate_round: 
    /// marks by adjudicator "initials", optionally structured in groups, 
    /// for each participant, then for each element of the round (e.g. dance)
    marks   : { element+ : { start_no+ : { adjudicators_group+ : { adjudicator_short+ : num } } } }, 
    /// placings from the individual round elements
    places  : { element+ : { start_no+ : { "pmin" : num, "pmax" : num, "pval" : num } } },
    /// optional parameters needed for evaluation
    params? : { param+ : num }
->
    /// optional data for result reports, optionally structured in groups
    report? : { start_no+ : { colum-tree+ : any } },            
    /// place range pmin..pmax and representative value pval;
    /// results will be ordered according to the latter
    places  : { start_no+ : { "pmin" : num, "pmax" : num, "pval" : num } };


// Import all adjudication libraries ===================================================================================

import cross-majority-skating as cms;
// import equality_classification as eq_cls;
// ...
