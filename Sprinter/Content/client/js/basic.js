/*
 * SimpleModal Basic Modal Dialog
 * http://www.ericmmartin.com/projects/simplemodal/
 * http://code.google.com/p/simplemodal/
 *
 * Copyright (c) 2010 Eric Martin - http://ericmmartin.com
 *
 * Licensed under the MIT license:
 *   http://www.opensource.org/licenses/mit-license.php
 *
 * Revision: $Id: basic.js 254 2010-07-23 05:14:44Z emartin24 $
 */

jQuery(function ($) {
	// Load dialog on page load
	//$('#basic-modal-content').modal();

	// Load dialog on click
	$('#basic-modal .basic').click(function (e) {
		$('#basic-modal-content').modal();

		return false;
	});
    
	$('#PassRemember').click(function (e) {
	    //{ containerCss: { width: 400, height: 200 } }
	    $('#rememberpass-modal-content').modal({ containerCss: { width: 400, height: 200 } });
	    return false;
	});
    
	$('.additional_tags .tag2').click(function (e) {
	    var height = $('#comment-modal-content').css('height').replace('px', '');
	    $('#comment-modal-content').modal({ containerCss: { width: 400, height: parseInt(height)+30 } });
	    return false;
	});

	$('.auth-link').click(function () {
	    $('#auth-modal-content').modal({ containerCss: { width: 400, height: 250 } });
	    $('.auth-redirect').val('cabinet');
        return false;
	});

	$('.reg a').click(function () {
	    $('#register-modal-content').modal({ containerCss: { width: 400, height: 250 } });
	    $('.auth-redirect').val('');
        return false;
	});

	$('.pass-lost').click(function () {
	    $.modal.close();
	    setTimeout(function() {
	        $('#rememberpass-modal-content').modal({ containerCss: { width: 400, height: 200 } });
	    }, 200);
        return false;
	});
    $('.auth-btn').click(function() {

    });
});